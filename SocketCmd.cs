using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Tecnomatix.Engineering;
using System.Collections.Generic;
using Tecnomatix.Engineering.Olp;
using System.Linq;

namespace ProcessSimulateSnippets
{
    public class SocketCmd : TxButtonCommand
    {
        public override string Category
        {
            get
            {
                return "Resource";
            }
        }

        public override string Name
        {
            get
            {
                return "Start Socket";
            }
        }

        public override void Execute(object cmdParams)
        {

            string address = "127.0.0.1";
            int port = 12345;
            int pause_ms = 2;
            int dim1 = 10;
            int dim2 = 20;

            SocketManager basic_socket = new SocketManager();
            basic_socket.BasicSocketTest(address, port, pause_ms, dim1, dim2);

        }
    }
}
