using System;
using System.IO;
using System.Reflection;
using Napa.Common.Utils;
using Napa.Drawables;
using Napa.Extensions;
using Napa.Scripting.Core;
using Napa.UI.Common.Dialogs;

namespace Napa.Hooks {

    /// <summary>
    /// Plugin for executing hooks scripts. Hook scripts are excuted when some event is raised.
    /// The scripts should be located to folder "Hooks" inside the same folder the plugin is placed.
    /// </summary>
    [Extension("DesignerHooks")]
    public class DesignerHooksExtension : IExtension {

        private IExtensionContext Context { get; set; }

        public void Initialize(IExtensionContext context) {
            Context = context;
            Context.ProjectEventSource.ProjectOpened += OnProjectOpened;
        }

        public void Dispose() {
            if (Context.CurrentProjectVersion == null) return;
            Context.CurrentProjectVersion.GeometryManager.GeometricObjectEntered -= OnObjectEntered;
        }

        private void OnObjectEntered(object o, EventArgs args) {
            
            var name = o.ToString();
            if (name.Contains("_TEMP_")) return;
            var version = Context.CurrentProjectVersion;
            var surfaceObject = version.GeometryManager.GetSurfaceObject(name);
            if (surfaceObject == null) return;
            string scriptName = "";
            //TODO better way to check that object is just created
            var isNew = DateTime.Now - surfaceObject.Date < TimeSpan.FromSeconds(3);
            if (isNew) { 
                //Begin invoke, seems that with big models the Entered event comes too early. 
                Alfred.ModelingWorkspaceVM.Designer.Dispatcher.BeginInvoke(new Action(() => {
                    try {
                        var path = Path.Combine(ProgramUtils.PathToAssembly(Assembly.GetExecutingAssembly()), "Hooks");
                        var scriptFiles = Directory.GetFiles(path, "*.cs");
                        foreach (var script in scriptFiles) {
                            scriptName = Path.GetFileName(script);
                            var hook = ScriptEngine.ExecuteFile<IHook>(script);
                            hook.Run(version, name);
                        }
                    } catch (Exception e) {
                        ModalDialog.ShowException(e, "Hook error " + scriptName);
                    }
                }));
            }
        }

        private void OnProjectOpened() {
            Context.CurrentProjectVersion.GeometryManager.GeometricObjectEntered += OnObjectEntered;
        }

    }
}
