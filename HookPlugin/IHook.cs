using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Napa.Hooks {

    public interface IHook {
        void Run(string name);
    }

}
