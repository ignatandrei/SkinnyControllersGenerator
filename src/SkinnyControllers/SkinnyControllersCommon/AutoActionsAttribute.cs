using System;

namespace SkinnyControllersCommon
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class AutoActionsAttribute : Attribute
    {
        public TemplateIndicator template { get; set; }
        public string[] FieldsName { get; set; }
        public string[] ExcludeFields { get; set; }
        public string CustomTemplateFileName { get; set; }

    }
}