using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Napa.Core.Project;

namespace Napa.Hooks {

    public interface IHook {
        void Run(IProjectVersion version, string name);
    }

}
