using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SPP4;


namespace AwesomeIgor
{
    public class IgorClass
    {
        int magic;

        [Logger.LoggerAttribute("")]
        public IgorClass(int magic)
        {
            this.magic = magic;
        }
    }
}
