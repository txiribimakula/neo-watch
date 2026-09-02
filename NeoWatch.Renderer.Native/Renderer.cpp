#include <windows.h>
#include <d3d11.h>
#include <d3d9.h>
#include <d2d1_1.h>
#include <dxgi1_2.h>
#include <wrl/client.h>
#include <array>
#include <vector>
#include <unordered_map>
#include <algorithm>
#include <cmath>
#include <chrono>
#include <stdexcept>
#include <string>
#include "GeometryVS.h"
#include "GeometryPS.h"
#include "CompositeVS.h"
#include "CompositePS.h"

using Microsoft::WRL::ComPtr;
static thread_local std::string lastError;
static void Check(HRESULT hr) { if (FAILED(hr)) throw hr; }
static constexpr double Pi = 3.14159265358979323846;

struct Primitive { double x, y, endX, endY, radius, start, sweep; int kind; };
static_assert(sizeof(Primitive) == 64, "Managed/native primitive ABI");
struct Instance { float coords[4], arc[4]; UINT index; };
struct Constants {
    float origin[2], scale, dpi;
    float viewport[2], thickness, dotSize;
    int selected, mode, singleItem, arcSpans;
    float color[4];
};
static_assert(sizeof(Constants) == 64, "Shader constant layout");
struct Block {
    std::array<ComPtr<ID3D11Buffer>, 3> buffers;
    std::array<UINT, 3> counts{};
    std::vector<std::pair<int, UINT>> selection;
    ComPtr<ID2D1PathGeometry> path;
    double originX = 0, originY = 0, maxCoordinate = 0, maxRadius = 0, error = 0;
};

class Renderer {
public:
    bool comparisonBackend;
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;
    ComPtr<IDirect3D9Ex> d3d9;
    ComPtr<IDirect3DDevice9Ex> device9;
    ComPtr<IDirect3DTexture9> texture9;
    ComPtr<IDirect3DSurface9> surface9;
    ComPtr<ID3D11Texture2D> output, mask;
    ComPtr<ID3D11RenderTargetView> outputView, maskView;
    ComPtr<ID3D11ShaderResourceView> maskResource;
    ComPtr<ID3D11VertexShader> vertexShader, fullVertexShader;
    ComPtr<ID3D11PixelShader> pixelShader, compositeShader;
    ComPtr<ID3D11InputLayout> layout;
    ComPtr<ID3D11Buffer> constants;
    ComPtr<ID3D11BlendState> maskBlend, compositeBlend;
    ComPtr<ID3D11RasterizerState> rasterizer;
    ComPtr<ID3D11Query> completion;
    ComPtr<ID2D1Factory1> d2dFactory;
    ComPtr<ID2D1Device> d2dDevice;
    ComPtr<ID2D1DeviceContext> d2dContext;
    ComPtr<ID2D1Bitmap1> d2dTarget;
    ComPtr<ID2D1SolidColorBrush> d2dBrush;
    ComPtr<ID2D1StrokeStyle1> fixedStroke;
    std::unordered_map<long long, Block> blocks;
    UINT width = 0, height = 0;
    HWND dummy = nullptr;
    Constants state{};
    double left = 0, bottom = 0, scale = 1;

    explicit Renderer(bool comparison) : comparisonBackend(comparison) {
        dummy = CreateWindowExW(0, L"STATIC", L"Neo Watch GPU", WS_POPUP, 0, 0, 1, 1,
            nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
        if (!dummy) Check(HRESULT_FROM_WIN32(GetLastError()));
        try { Initialize(); } catch (...) { DestroyWindow(dummy); dummy = nullptr; throw; }
    }
    ~Renderer() { if (dummy) DestroyWindow(dummy); }

    void Initialize() {
        Check(Direct3DCreate9Ex(D3D_SDK_VERSION, &d3d9));
        LUID luid{}; Check(d3d9->GetAdapterLUID(D3DADAPTER_DEFAULT, &luid));
        ComPtr<IDXGIFactory1> factory; Check(CreateDXGIFactory1(IID_PPV_ARGS(&factory)));
        ComPtr<IDXGIAdapter1> adapter;
        for (UINT i = 0; ; i++) {
            Check(factory->EnumAdapters1(i, &adapter));
            DXGI_ADAPTER_DESC1 desc{}; Check(adapter->GetDesc1(&desc));
            if (desc.AdapterLuid.HighPart == luid.HighPart && desc.AdapterLuid.LowPart == luid.LowPart) break;
            adapter.Reset();
        }
        D3D_FEATURE_LEVEL level;
        const D3D_FEATURE_LEVEL requested[] = { D3D_FEATURE_LEVEL_11_0 };
        Check(D3D11CreateDevice(adapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT, requested, 1, D3D11_SDK_VERSION, &device, &level, &context));
        D3DPRESENT_PARAMETERS present{};
        present.Windowed = TRUE; present.SwapEffect = D3DSWAPEFFECT_DISCARD;
        present.hDeviceWindow = dummy; present.BackBufferWidth = present.BackBufferHeight = 1;
        Check(d3d9->CreateDeviceEx(D3DADAPTER_DEFAULT, D3DDEVTYPE_HAL, dummy,
            D3DCREATE_HARDWARE_VERTEXPROCESSING | D3DCREATE_MULTITHREADED | D3DCREATE_FPU_PRESERVE,
            &present, nullptr, &device9));
        Check(device->CreateVertexShader(GeometryVSBytes, sizeof(GeometryVSBytes), nullptr, &vertexShader));
        Check(device->CreatePixelShader(GeometryPSBytes, sizeof(GeometryPSBytes), nullptr, &pixelShader));
        Check(device->CreateVertexShader(CompositeVSBytes, sizeof(CompositeVSBytes), nullptr, &fullVertexShader));
        Check(device->CreatePixelShader(CompositePSBytes, sizeof(CompositePSBytes), nullptr, &compositeShader));
        const D3D11_INPUT_ELEMENT_DESC elements[] = {
            {"POSITION",0,DXGI_FORMAT_R32G32B32A32_FLOAT,0,0,D3D11_INPUT_PER_INSTANCE_DATA,1},
            {"TEXCOORD",0,DXGI_FORMAT_R32G32B32A32_FLOAT,0,16,D3D11_INPUT_PER_INSTANCE_DATA,1},
            {"TEXCOORD",1,DXGI_FORMAT_R32_UINT,0,32,D3D11_INPUT_PER_INSTANCE_DATA,1}
        };
        Check(device->CreateInputLayout(elements, 3, GeometryVSBytes, sizeof(GeometryVSBytes), &layout));
        D3D11_BUFFER_DESC buffer{}; buffer.ByteWidth = sizeof(Constants); buffer.Usage = D3D11_USAGE_DEFAULT;
        buffer.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
        Check(device->CreateBuffer(&buffer, nullptr, &constants));
        D3D11_BLEND_DESC blend{}; auto& rt = blend.RenderTarget[0];
        rt.BlendEnable = TRUE; rt.SrcBlend = rt.DestBlend = D3D11_BLEND_ONE;
        rt.BlendOp = rt.BlendOpAlpha = D3D11_BLEND_OP_MAX;
        rt.SrcBlendAlpha = rt.DestBlendAlpha = D3D11_BLEND_ONE;
        rt.RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
        Check(device->CreateBlendState(&blend, &maskBlend));
        rt.BlendOp = rt.BlendOpAlpha = D3D11_BLEND_OP_ADD;
        rt.DestBlend = rt.DestBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA;
        Check(device->CreateBlendState(&blend, &compositeBlend));
        D3D11_RASTERIZER_DESC raster{}; raster.FillMode = D3D11_FILL_SOLID;
        raster.CullMode = D3D11_CULL_NONE; raster.DepthClipEnable = TRUE;
        Check(device->CreateRasterizerState(&raster, &rasterizer));
        D3D11_QUERY_DESC query{D3D11_QUERY_EVENT,0}; Check(device->CreateQuery(&query, &completion));
        if (!comparisonBackend) return;
        Check(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, d2dFactory.GetAddressOf()));
        ComPtr<IDXGIDevice> dxgiDevice; Check(device.As(&dxgiDevice));
        Check(d2dFactory->CreateDevice(dxgiDevice.Get(), &d2dDevice));
        Check(d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &d2dContext));
        Check(d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Black), &d2dBrush));
        D2D1_STROKE_STYLE_PROPERTIES1 style{};
        style.transformType = D2D1_STROKE_TRANSFORM_TYPE_FIXED;
        Check(d2dFactory->CreateStrokeStyle(style, nullptr, 0, &fixedStroke));
    }

    void Resize(UINT w, UINT h) {
        if (w == width && h == height) return;
        if (!w || !h || w > 8192 || h > 8192 || uint64_t(w)*h > 16777216) Check(E_INVALIDARG);
        context->ClearState(); if (d2dContext) d2dContext->SetTarget(nullptr); d2dTarget.Reset();
        outputView.Reset(); maskView.Reset(); maskResource.Reset(); mask.Reset(); output.Reset();
        surface9.Reset(); texture9.Reset(); width = height = 0;
        HANDLE shared = nullptr;
        Check(device9->CreateTexture(w,h,1,D3DUSAGE_RENDERTARGET,D3DFMT_A8R8G8B8,D3DPOOL_DEFAULT,&texture9,&shared));
        Check(texture9->GetSurfaceLevel(0,&surface9));
        Check(device->OpenSharedResource(shared,IID_PPV_ARGS(&output)));
        Check(device->CreateRenderTargetView(output.Get(),nullptr,&outputView));
        D3D11_TEXTURE2D_DESC desc{}; desc.Width=w; desc.Height=h; desc.MipLevels=desc.ArraySize=1;
        desc.Format=DXGI_FORMAT_R8_UNORM; desc.SampleDesc.Count=1;
        desc.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
        Check(device->CreateTexture2D(&desc,nullptr,&mask));
        Check(device->CreateRenderTargetView(mask.Get(),nullptr,&maskView));
        Check(device->CreateShaderResourceView(mask.Get(),nullptr,&maskResource));
        width=w; height=h;
        if (!comparisonBackend) return;
        ComPtr<IDXGISurface> dxgiSurface; Check(output.As(&dxgiSurface));
        auto properties=D2D1::BitmapProperties1(D2D1_BITMAP_OPTIONS_TARGET|D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
            D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM,D2D1_ALPHA_MODE_PREMULTIPLIED));
        Check(d2dContext->CreateBitmapFromDxgiSurface(dxgiSurface.Get(),&properties,&d2dTarget));
        d2dContext->SetTarget(d2dTarget.Get()); width=w; height=h;
    }

    void Upload(long long id, const Primitive* primitives, int count) {
        if (!primitives || count <= 0 || count > 2048) Check(E_INVALIDARG);
        Block block; block.originX=primitives[0].x; block.originY=primitives[0].y;
        block.selection.resize(count, {-1,0});
        std::array<std::vector<Instance>,3> instances;
        ComPtr<ID2D1GeometrySink> sink;
        if (comparisonBackend) {
            Check(d2dFactory->CreatePathGeometry(&block.path));
            Check(block.path->Open(&sink));
        }
        for (int i=0;i<count;i++) {
            const auto& p=primitives[i];
            if (p.kind<0 || p.kind>2 || !std::isfinite(p.x) || !std::isfinite(p.y)
                || !std::isfinite(p.endX) || !std::isfinite(p.endY) || !std::isfinite(p.radius)
                || !std::isfinite(p.start) || !std::isfinite(p.sweep) || p.radius<0) Check(E_INVALIDARG);
            Instance instance{}; instance.index=i;
            double values[]={p.x-block.originX,p.y-block.originY,
                p.kind==1?p.endX-block.originX:0,p.kind==1?p.endY-block.originY:0};
            for (int j=0;j<4;j++) {
                instance.coords[j]=static_cast<float>(values[j]);
                block.error=std::max(block.error,std::abs(values[j]-instance.coords[j]));
                block.maxCoordinate=std::max(block.maxCoordinate,std::abs(values[j]));
            }
            const bool circle=std::abs(p.sweep)>=360;
            instance.arc[0]=static_cast<float>(p.radius);
            instance.arc[1]=static_cast<float>((circle?0:std::remainder(p.start,360))*Pi/180);
            instance.arc[2]=static_cast<float>((circle?360:p.sweep)*Pi/180);
            instance.arc[3]=static_cast<float>(p.kind);
            block.maxCoordinate=std::max(block.maxCoordinate,p.radius);
            block.maxRadius=std::max(block.maxRadius,p.radius);
            block.error=std::max(block.error,std::abs(p.radius-instance.arc[0]));
            if (p.kind==2 && !circle) {
                // The existing domain stores arc endpoints as floats. Do not silently replace
                // those endpoints with a visibly different ideal arc at extreme coordinates.
                for (int end=0;end<2;end++) {
                    double a=((p.start+(end?p.sweep:0))/180)*Pi;
                    double normalized=instance.arc[1]+(end?instance.arc[2]:0);
                    double x=p.x+cos(normalized)*p.radius, y=p.y+sin(normalized)*p.radius;
                    float oldX=static_cast<float>(p.x)+static_cast<float>(cos(a))*static_cast<float>(p.radius);
                    float oldY=static_cast<float>(p.y)+static_cast<float>(sin(a))*static_cast<float>(p.radius);
                    block.error=std::max(block.error,std::max(std::abs(x-oldX),std::abs(y-oldY)));
                }
            }
            if (p.kind==2 && (p.radius==0 || p.sweep==0)) continue;
            block.selection[i]={p.kind,static_cast<UINT>(instances[p.kind].size())};
            instances[p.kind].push_back(instance);
            if (sink && p.kind==1) {
                sink->BeginFigure(D2D1::Point2F(instance.coords[0],instance.coords[1]),D2D1_FIGURE_BEGIN_HOLLOW);
                sink->AddLine(D2D1::Point2F(instance.coords[2],instance.coords[3]));
                sink->EndFigure(D2D1_FIGURE_END_OPEN);
            } else if (sink && p.kind==2) {
                int pieces=circle?2:1;
                for (int j=0;j<pieces;j++) {
                    double a=circle?j*Pi:instance.arc[1], sweep=circle?Pi:instance.arc[2];
                    const float x=instance.coords[0], y=instance.coords[1], r=instance.arc[0];
                    sink->BeginFigure(D2D1::Point2F(x+r*static_cast<float>(cos(a)),y+r*static_cast<float>(sin(a))),D2D1_FIGURE_BEGIN_HOLLOW);
                    sink->AddArc(D2D1::ArcSegment(D2D1::Point2F(x+r*static_cast<float>(cos(a+sweep)),y+r*static_cast<float>(sin(a+sweep))),
                        D2D1::SizeF(r,r),0,sweep>0?D2D1_SWEEP_DIRECTION_CLOCKWISE:D2D1_SWEEP_DIRECTION_COUNTER_CLOCKWISE,
                        std::abs(sweep)>=Pi?D2D1_ARC_SIZE_LARGE:D2D1_ARC_SIZE_SMALL));
                    sink->EndFigure(D2D1_FIGURE_END_OPEN);
                }
            }
        }
        if (sink) Check(sink->Close());
        for (int kind=0;kind<3;kind++) {
            block.counts[kind]=static_cast<UINT>(instances[kind].size());
            if (!block.counts[kind]) continue;
            D3D11_BUFFER_DESC desc{}; desc.ByteWidth=sizeof(Instance)*block.counts[kind];
            desc.Usage=D3D11_USAGE_IMMUTABLE; desc.BindFlags=D3D11_BIND_VERTEX_BUFFER;
            D3D11_SUBRESOURCE_DATA data{}; data.pSysMem=instances[kind].data();
            Check(device->CreateBuffer(&desc,&data,&block.buffers[kind]));
        }
        blocks.insert_or_assign(id,std::move(block));
    }

    void Begin(double x, double y, double zoom, float dpi) {
        if (!width || !std::isfinite(x) || !std::isfinite(y) || !std::isfinite(zoom) || zoom<=0) Check(E_INVALIDARG);
        left=x; bottom=y; scale=zoom;
        state.viewport[0]=static_cast<float>(width); state.viewport[1]=static_cast<float>(height);
        state.scale=static_cast<float>(zoom); state.dpi=dpi;
        const float clear[]={0,0,0,0}; context->ClearRenderTargetView(outputView.Get(),clear);
        D3D11_VIEWPORT viewport{0,0,static_cast<float>(width),static_cast<float>(height),0,1};
        context->RSSetViewports(1,&viewport); context->RSSetState(rasterizer.Get());
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->VSSetConstantBuffers(0,1,constants.GetAddressOf());
        context->PSSetConstantBuffers(0,1,constants.GetAddressOf());
    }

    void Layer(const long long* ids, const int* offsets, int count, int selected, int mode,
        bool single, float thickness, float dot, unsigned color, float opacity, bool direct2D) {
        if (count<0 || (count && (!ids || !offsets)) || mode<0 || mode>4) Check(E_INVALIDARG);
        if (direct2D && !comparisonBackend) Check(E_NOTIMPL);
        if (!count) return;
        bool draws=false;
        for (int i=0;i<count;i++) {
            auto it=blocks.find(ids[i]); if (it==blocks.end()) Check(E_INVALIDARG);
            const auto& block=it->second;
            int local=selected<0?-1:selected-offsets[i];
            int selectedKind=local>=0 && local<static_cast<int>(block.selection.size()) ? block.selection[local].first : -1;
            bool points=mode==3 || mode==4;
            bool matches=selectedKind>=0 && (points ? selectedKind==0 : selectedKind!=0);
            int active=points?block.counts[0]:mode==2?block.counts[1]:block.counts[1]+block.counts[2];
            if (mode==1 || mode==4) active=matches?1:0;
            else if (mode!=2 && !single && matches) active--;
            draws|=active>0;
        }
        if (!draws || !(color>>24) || opacity<=0) return;
        state.mode=mode; state.singleItem=single; state.thickness=thickness*state.dpi; state.dotSize=dot*state.dpi;
        state.color[0]=((color>>16)&255)/255.f; state.color[1]=((color>>8)&255)/255.f;
        state.color[2]=(color&255)/255.f; state.color[3]=opacity*((color>>24)&255)/255.f;
        const float clear[]={0,0,0,0}; context->ClearRenderTargetView(maskView.Get(),clear);
        ID3D11ShaderResourceView* none=nullptr; context->PSSetShaderResources(0,1,&none);
        context->OMSetRenderTargets(1,maskView.GetAddressOf(),nullptr);
        context->OMSetBlendState(maskBlend.Get(),nullptr,0xffffffff);
        context->IASetInputLayout(layout.Get()); context->VSSetShader(vertexShader.Get(),nullptr,0);
        context->PSSetShader(pixelShader.Get(),nullptr,0);
        for (int i=0;i<count;i++) {
            auto it=blocks.find(ids[i]); if (it==blocks.end()) Check(E_INVALIDARG);
            auto& block=it->second;
            const int localSelected=selected<0?-1:selected-offsets[i];
            const bool onlySelected=mode==1 || mode==4;
            if (onlySelected && (localSelected<0 || localSelected>=static_cast<int>(block.selection.size()))) continue;
            // Bound upload and arithmetic error in screen pixels; never silently draw a low-precision frame.
            double error=(block.error+block.maxCoordinate*0.00000048)*scale;
            double originX=(block.originX-left)*scale, originY=height-(block.originY-bottom)*scale;
            error+=std::max(std::abs(originX),std::abs(originY))*0.00000012;
            if (!std::isfinite(error) || error>0.2) {
                lastError="Canvas coordinates exceed the 0.2 pixel precision budget.";
                Check(E_NOTIMPL);
            }
            state.origin[0]=static_cast<float>(originX); state.origin[1]=static_cast<float>(originY);
            state.selected=localSelected;
            state.arcSpans=std::clamp(static_cast<int>(std::ceil(Pi*std::sqrt(block.maxRadius*scale))),8,512);
            context->UpdateSubresource(constants.Get(),0,nullptr,&state,0,0);
            if (direct2D && mode==0) {
                // Diagnostic comparison only; production uses a single union mask per layer.
                context->OMSetRenderTargets(0,nullptr,nullptr);
                d2dContext->BeginDraw();
                d2dContext->SetTransform(D2D1::Matrix3x2F(state.scale,0,0,-state.scale,state.origin[0],state.origin[1]));
                d2dBrush->SetColor(D2D1::ColorF(state.color[0],state.color[1],state.color[2],state.color[3]));
                d2dContext->DrawGeometry(block.path.Get(),d2dBrush.Get(),state.thickness,fixedStroke.Get());
                Check(d2dContext->EndDraw());
                continue;
            }
            for (int kind=0;kind<3;kind++) {
                if (!block.counts[kind]) continue;
                if ((mode==3 || mode==4) != (kind==0)) continue;
                if (mode==2 && kind!=1) continue;
                if (onlySelected && block.selection[localSelected].first!=kind) continue;
                UINT stride=sizeof(Instance), offset=0;
                context->IASetVertexBuffers(0,1,block.buffers[kind].GetAddressOf(),&stride,&offset);
                context->DrawInstanced(kind==2?6*state.arcSpans:6,onlySelected?1:block.counts[kind],0,
                    onlySelected?block.selection[localSelected].second:0);
            }
        }
        if (direct2D && mode==0) return;
        context->OMSetRenderTargets(1,outputView.GetAddressOf(),nullptr);
        context->OMSetBlendState(compositeBlend.Get(),nullptr,0xffffffff);
        context->IASetInputLayout(nullptr); context->VSSetShader(fullVertexShader.Get(),nullptr,0);
        context->PSSetShader(compositeShader.Get(),nullptr,0);
        context->PSSetShaderResources(0,1,maskResource.GetAddressOf()); context->Draw(3,0);
        context->PSSetShaderResources(0,1,&none);
    }

    void Finish() {
        context->End(completion.Get()); context->Flush();
        // D3D9/WPF cannot consume a shared D3D11 surface until GPU writes have completed.
        // A bounded wait is preferable to handing WPF a partially rendered or stale frame.
        auto start=std::chrono::steady_clock::now();
        HRESULT hr;
        while ((hr=context->GetData(completion.Get(),nullptr,0,0))==S_FALSE) {
            if (std::chrono::steady_clock::now()-start>std::chrono::milliseconds(250)) Check(DXGI_ERROR_DEVICE_HUNG);
            SwitchToThread();
        }
        Check(hr); Check(device->GetDeviceRemovedReason());
    }
};

#define API extern "C" __declspec(dllexport) HRESULT __cdecl
extern "C" __declspec(dllexport) int __cdecl nw_abi_version() { return 1; }
extern "C" __declspec(dllexport) const char* __cdecl nw_error() { return lastError.c_str(); }
#define GUARD_BEGIN lastError.clear(); try {
#define GUARD_END } catch (HRESULT hr) { return hr; } catch (const std::bad_alloc&) { return E_OUTOFMEMORY; } catch (...) { return E_FAIL; } return S_OK;
API nw_create(Renderer** result, int comparison) { GUARD_BEGIN if (!result) return E_POINTER; *result=nullptr; *result=new Renderer(comparison!=0); GUARD_END }
API nw_destroy(Renderer* renderer) { delete renderer; return S_OK; }
API nw_resize(Renderer* renderer, UINT width, UINT height, IDirect3DSurface9** surface) {
    GUARD_BEGIN if (!renderer || !surface) return E_POINTER; renderer->Resize(width,height); *surface=renderer->surface9.Get(); GUARD_END
}
API nw_upload(Renderer* renderer, long long id, const Primitive* primitives, int count) {
    GUARD_BEGIN if (!renderer) return E_POINTER; renderer->Upload(id,primitives,count); GUARD_END
}
API nw_release(Renderer* renderer, long long id) { GUARD_BEGIN if (!renderer) return E_POINTER; renderer->blocks.erase(id); GUARD_END }
API nw_begin(Renderer* renderer, double left, double bottom, double scale, float dpi) {
    GUARD_BEGIN if (!renderer) return E_POINTER; renderer->Begin(left,bottom,scale,dpi); GUARD_END
}
API nw_layer(Renderer* renderer, const long long* ids, const int* offsets, int count, int selected, int mode,
    int single, float thickness, float dot, unsigned color, float opacity, int direct2D) {
    GUARD_BEGIN if (!renderer) return E_POINTER;
    renderer->Layer(ids,offsets,count,selected,mode,single!=0,thickness,dot,color,opacity,direct2D!=0); GUARD_END
}
API nw_end(Renderer* renderer) { GUARD_BEGIN if (!renderer) return E_POINTER; renderer->Finish(); GUARD_END }
// Readback is exclusively for the offline pixel-contract harness, never for WPF presentation.
API nw_read_pixels(Renderer* renderer, void* pixels, int size) {
    GUARD_BEGIN
    if (!renderer || !pixels || size<static_cast<int>(renderer->width*renderer->height*4)) return E_INVALIDARG;
    D3D11_TEXTURE2D_DESC desc{}; renderer->output->GetDesc(&desc);
    desc.Usage=D3D11_USAGE_STAGING; desc.BindFlags=0; desc.MiscFlags=0; desc.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
    ComPtr<ID3D11Texture2D> staging; Check(renderer->device->CreateTexture2D(&desc,nullptr,&staging));
    renderer->context->CopyResource(staging.Get(),renderer->output.Get());
    D3D11_MAPPED_SUBRESOURCE mapped{}; Check(renderer->context->Map(staging.Get(),0,D3D11_MAP_READ,0,&mapped));
    for (UINT y=0;y<renderer->height;y++) memcpy(static_cast<char*>(pixels)+y*renderer->width*4,
        static_cast<char*>(mapped.pData)+y*mapped.RowPitch,renderer->width*4);
    renderer->context->Unmap(staging.Get(),0);
    GUARD_END
}
