﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ConvertHero.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("ConvertHero.Properties.Resources", typeof(Resources).Assembly);
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
        ///   Looks up a localized string similar to [Events]
        ///{{
        ///{0}
        ///}}.
        /// </summary>
        internal static string EventsTrack {
            get {
                return ResourceManager.GetString("EventsTrack", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [{0}]
        ///{{
        ///{1}
        ///}}.
        /// </summary>
        internal static string NoteTrack {
            get {
                return ResourceManager.GetString("NoteTrack", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [Song]
        ///{{
        ///  Name = &quot;&quot;
        ///  Artist = &quot;&quot;
        ///  Charter = &quot;&quot;
        ///  Offset = 0
        ///  Resolution = {0}
        ///  Player2 = bass
        ///  Difficulty = 0
        ///  PreviewStart = 0
        ///  PreviewEnd = 0
        ///  Genre = &quot;&quot;
        ///  MediaType = &quot;&quot;
        ///  MusicStream = &quot;{1}&quot;
        ///}}.
        /// </summary>
        internal static string SongSection {
            get {
                return ResourceManager.GetString("SongSection", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [SyncTrack]
        ///{{
        ///{0}
        ///}}.
        /// </summary>
        internal static string SyncTrack {
            get {
                return ResourceManager.GetString("SyncTrack", resourceCulture);
            }
        }
    }
}
