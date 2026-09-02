cbuffer Camera : register(b0) {
    float2 origin; float scale; float dpi;
    float2 viewport; float thickness; float dotSize;
    int selected; int mode; int singleItem; int arcSpans;
    float4 color;
};
struct Instance {
    float4 coords : POSITION;
    float4 arc : TEXCOORD0;
    uint index : TEXCOORD1;
};
struct Pixel {
    float4 position : SV_POSITION;
    nointerpolation float4 coords : TEXCOORD0;
    nointerpolation float4 arc : TEXCOORD1;
    nointerpolation uint kind : TEXCOORD2;
    nointerpolation uint enabled : TEXCOORD3;
};
float2 corner(uint vertex) {
    const float2 corners[6] = { float2(0,0),float2(1,0),float2(0,1),float2(0,1),float2(1,0),float2(1,1) };
    return corners[vertex % 6];
}
float2 screen(float2 p) { return origin + float2(p.x, -p.y) * scale; }
float4 clip(float2 p) { return float4(p.x / viewport.x * 2 - 1, 1 - p.y / viewport.y * 2, 0, 1); }
Pixel VS(Instance input, uint vertex : SV_VertexID) {
    Pixel output;
    uint kind = (uint)input.arc.w;
    bool isSelected = (int)input.index == selected;
    bool selectionLayer = mode == 1 || mode == 4;
    bool pointLayer = mode == 3 || mode == 4;
    bool enabled = mode == 2 ? kind == 1 : (pointLayer == (kind == 0));
    enabled = enabled && (mode == 2 || (selectionLayer ? isSelected : (!isSelected || singleItem != 0)));
    float2 start = screen(input.coords.xy);
    float2 end = screen(input.coords.zw);
    float2 low = min(start,end), high = max(start,end);
    float radius = input.arc.x * scale;
    float margin = thickness * 0.5 + 1;
    if (kind == 0) { low = start; high = start; margin = dotSize * 0.5 + 1; }
    if (mode == 2) { low = end - 8 * dpi; high = end + 8 * dpi; margin = 1; }
    if (kind == 2) {
        // Camera-dependent analytic spans bound overdraw without rebuilding instance buffers.
        uint span = vertex / 6;
        float sweep = input.arc.z;
        float a = input.arc.y + sweep * span / arcSpans;
        float b = input.arc.y + sweep * (span + 1) / arcSpans;
        float2 p = float2(cos(a), -sin(a));
        float2 q = float2(cos(b), -sin(b));
        // The sagitta expansion is conservative for spans <= 45 degrees, including negative sweeps.
        float sagitta = radius * (1 - cos(abs(b-a) * 0.5));
        low = start + radius * min(p,q) - sagitta;
        high = start + radius * max(p,q) + sagitta;
    }
    float2 p = lerp(low-margin, high+margin, corner(vertex));
    if (kind == 1 && mode != 2) {
        float2 delta = end-start;
        float len = length(delta);
        float2 dir = len > 0.00001 ? delta/len : float2(1,0);
        float2 uv = corner(vertex);
        p = start + dir*lerp(-margin,len+margin,uv.x)
            + float2(-dir.y,dir.x)*lerp(-margin,margin,uv.y);
    }
    output.position = enabled ? clip(p) : float4(2,2,0,1);
    output.coords = float4(start,end);
    output.arc = float4(radius,input.arc.y,input.arc.z,0);
    output.kind = kind; output.enabled = enabled;
    return output;
}
float Coverage(float signedDistance) { return saturate(0.5 - signedDistance); }
float PS(Pixel input) : SV_TARGET {
    if (input.enabled == 0) discard;
    float2 p = input.position.xy;
    float halfWidth = thickness * 0.5;
    bool dashed = mode == 1;
    float distance;
    if (input.kind == 0) {
        float2 delta = abs(p - input.coords.xy) - dotSize * 0.5;
        return Coverage(max(delta.x,delta.y));
    }
    if (input.kind == 1) {
        float2 v = input.coords.zw - input.coords.xy;
        float len = sqrt(dot(v,v));
        if (len < 0.00001) {
            if (halfWidth <= dpi || mode == 2) discard;
            return Coverage(length(p-input.coords.xy) - halfWidth);
        }
        float2 dir = v / len;
        float along = dot(p-input.coords.xy,dir);
        float across = abs(dot(p-input.coords.xy,float2(-dir.y,dir.x)));
        if (mode == 2) {
            float back = len - along;
            // WPF's 7-DIP triangle cap after a 2-DIP segment.
            float rectangle = max(abs(back-dpi) - dpi, across - 3.5*dpi);
            float tip = max(max(-back-3.5*dpi, back), across - back - 3.5*dpi);
            return Coverage(min(rectangle,tip));
        }
        float outside = max(-along,along-len);
        distance = thickness > 2*dpi ? length(float2(max(outside,0),across))-halfWidth
            : max(outside,across-halfWidth);
        if (dashed) {
            float phase = along / thickness;
            phase = phase-floor(phase/5)*5;
            float gap = (abs(phase-1.5)-1.5)*thickness;
            if (thickness > 2*dpi) {
                gap = min(gap,(5-phase)*thickness);
                distance = max(distance,length(float2(max(gap,0),across))-halfWidth);
            } else distance = max(distance,gap);
        }
    } else {
        float2 v = p-input.coords.xy;
        float radial = length(v);
        float angle = atan2(-v.y,v.x);
        const float tau = 6.283185307179586;
        float sweep = input.arc.z;
        float delta = sweep >= 0 ? angle-input.arc.y : input.arc.y-angle;
        delta = delta - floor(delta/tau)*tau;
        float extent = abs(sweep);
        float radius = input.arc.x;
        float along = delta * radius;
        float total = extent * radius;
        float outside = delta <= extent ? -min(along,total-along)
            : min(delta-extent,tau-delta)*radius;
        distance = max(abs(radial-radius)-halfWidth,outside);
        if (thickness > 2*dpi && outside > 0) {
            float a = input.arc.y, b = a+sweep;
            float2 start = input.coords.xy + radius * float2(cos(a),-sin(a));
            float2 end = input.coords.xy + radius * float2(cos(b),-sin(b));
            distance = min(length(p-start),length(p-end))-halfWidth;
        }
        if (extent >= tau-0.00001) {
            distance = abs(radial-radius)-halfWidth;
            // The reference splits circles into two semicircles; restart its dash at pi.
            along = (delta % 3.141592653589793) * radius;
        }
        if (dashed) {
            float phase = (along/thickness) % 5;
            float gap = (abs(phase-1.5)-1.5)*thickness;
            if (thickness > 2*dpi && phase>3) {
                float offset = phase<4 ? (3-phase)*thickness : (5-phase)*thickness;
                float capAngle = input.arc.y + (sweep>=0?1:-1)*(delta+offset/max(radius,0.00001));
                float2 capPoint = input.coords.xy + radius*float2(cos(capAngle),-sin(capAngle));
                distance = max(distance,length(p-capPoint)-halfWidth);
            } else if (thickness <= 2*dpi) distance = max(distance,gap);
        }
    }
    return Coverage(distance);
}
Texture2D<float> mask : register(t0);
float4 FullVS(uint id : SV_VertexID) : SV_POSITION {
    float2 uv = float2((id << 1) & 2, id & 2);
    return float4(uv*float2(2,-2)+float2(-1,1),0,1);
}
float4 CompositePS(float4 p : SV_POSITION) : SV_TARGET {
    float a = mask.Load(int3(p.xy,0)) * color.a;
    return float4(color.rgb*a,a);
}
