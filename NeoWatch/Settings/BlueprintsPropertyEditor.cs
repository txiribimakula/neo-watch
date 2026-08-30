using System;
using System.ComponentModel;
using System.Drawing.Design;

namespace NeoWatch.Settings
{
    public sealed class BlueprintsPropertyEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            var window = new BlueprintEditorWindow(value as string);
            return window.ShowModal() == true ? window.BlueprintText : value;
        }
    }
}
