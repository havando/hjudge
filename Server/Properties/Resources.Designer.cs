﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Server.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Server.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;GroupBox Header=&quot;编译器 #${index}&quot; Height=&quot;162&quot; Width=&quot;460&quot; Name=&quot;CompilerConfig${index}&quot; xmlns=&quot;http://schemas.microsoft.com/winfx/2006/xaml/presentation&quot;
        ///                      xmlns:x=&quot;http://schemas.microsoft.com/winfx/2006/xaml&quot; xmlns:d=&quot;http://schemas.microsoft.com/expression/blend/2008&quot;
        ///                      xmlns:mc=&quot;http://schemas.openxmlformats.org/markup-compatibility/2006&quot; xmlns:local=&quot;clr-namespace:Server&quot; mc:Ignorable=&quot;d&quot;&gt;
        ///                &lt;Grid&gt;
        ///                    &lt;Label Content=&quot;编译器：&quot; Horiz [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string CompilerSetControl {
            get {
                return ResourceManager.GetString("CompilerSetControl", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;Grid Name=&quot;Data${index}&quot; Height=&quot;125&quot; Width=&quot;351&quot; xmlns=&quot;http://schemas.microsoft.com/winfx/2006/xaml/presentation&quot;
        ///        xmlns:x=&quot;http://schemas.microsoft.com/winfx/2006/xaml&quot;
        ///        xmlns:d=&quot;http://schemas.microsoft.com/expression/blend/2008&quot;
        ///        xmlns:mc=&quot;http://schemas.openxmlformats.org/markup-compatibility/2006&quot;
        ///        xmlns:local=&quot;clr-namespace:Server&quot;
        ///        mc:Ignorable=&quot;d&quot;&gt;
        ///                            &lt;Label Content=&quot;输入文件：&quot; HorizontalAlignment=&quot;Left&quot; Margin=&quot;10,36,0,0&quot; VerticalAlignment= [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string DataSetControl {
            get {
                return ResourceManager.GetString("DataSetControl", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Icon similar to (Icon).
        /// </summary>
        internal static System.Drawing.Icon Server {
            get {
                object obj = ResourceManager.GetObject("Server", resourceCulture);
                return ((System.Drawing.Icon)(obj));
            }
        }
    }
}
