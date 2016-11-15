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

        [Logger.LoggerAttribute("")]
        public bool IsAwesome()
        {
            if (magic == 69)
                return true;
            else
                return false;
        }

        [Logger.LoggerAttribute("")]
        public int HowSmart()
        {
            return 100;
        }


    }
}
