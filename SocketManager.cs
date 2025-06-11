using System;
using System.Net;
using System.Net.Sockets;
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
    class SocketManager
    {
        public void BasicSocketTest(string address, int port, int pause_ms, int dim1, int dim2)
        {
            TcpListener server = null;
            TcpClient client = null;
            NetworkStream stream = null;
            StringWriter output = new StringWriter();
            try
            {

                // Initialize some variables
                double[] vec1 = new double[dim1];
                double[] vec2 = new double[dim2];

                // Start listening for incoming connections
                server = new TcpListener(IPAddress.Parse(address), port);
                server.Start();

                // Accept a client connection
                client = server.AcceptTcpClient();
                stream = client.GetStream();

                // Receive the shared data
                var shared_data = ReceiveNumpyArray(stream);
                int Nsim = shared_data[0, 0];
                int trigger_end = shared_data[0, 1];
                int nested_idx = shared_data[0, 2];
                int loop_idx = shared_data[0, 3];
                System.Threading.Thread.Sleep(pause_ms);

                // Loop for all the simulations
                for (int jj = trigger_end; jj < Nsim - 1; jj++)
                {

                    // Receive sequence array
                    var sequence = ReceiveNumpyArray(stream);
                    System.Threading.Thread.Sleep(pause_ms);

                    // Receive shared array
                    var tasks = ReceiveNumpyArray(stream);
                    System.Threading.Thread.Sleep(pause_ms);

                    // Receive starting_times array
                    var starting_times = ReceiveNumpyArray(stream);
                    System.Threading.Thread.Sleep(pause_ms);

                    // re-initialize the index
                    nested_idx = shared_data[0, 2];

                    // Inner loop
                    while (nested_idx <= loop_idx)
                    {
                        // Receive the shared data
                        var test_vec = ReceiveNumpyArray(stream);
                        System.Threading.Thread.Sleep(pause_ms);

                        // Update the index
                        nested_idx++;

                        // Send the index back to the client
                        string updated_idx = (nested_idx).ToString();
                        byte[] updated_idx_ready = Encoding.ASCII.GetBytes(updated_idx);
                        stream.Write(updated_idx_ready, 0, updated_idx_ready.Length);

                    }

                    System.Threading.Thread.Sleep(pause_ms);

                    // Fake execution of the code
                    for (int ii = 0; ii < dim2; ii++)
                    {
                        if (ii < dim1)
                        {
                            vec1[ii] = (ii * 100) + jj;
                        }
                        vec2[ii] = (ii * 200) + jj;
                    }

                    // Send the first array back to the client
                    string result1_vec = string.Join(",", vec1);
                    byte[] result1 = Encoding.ASCII.GetBytes(result1_vec);
                    stream.Write(result1, 0, result1.Length);
                    System.Threading.Thread.Sleep(pause_ms);

                    // Send the second array back to the client
                    string result2_vec = string.Join(",", vec2);
                    byte[] result2 = Encoding.ASCII.GetBytes(result2_vec);
                    stream.Write(result2, 0, result2.Length);
                    System.Threading.Thread.Sleep(pause_ms);

                }

                // Close all the instances
                stream.Close();
                client.Close();
                server.Stop();
            }
            catch (Exception e) // Close the communication
            {
                TxMessageBox.Show(e.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Information);
                stream.Close();
                client.Close();
                server.Stop();
            }
        }

        static int[,] ReceiveNumpyArray(NetworkStream stream)
        {
            // Receive the shape of the array
            byte[] shapeBuffer = new byte[8]; // Assuming the shape is of two int32 values
            stream.Read(shapeBuffer, 0, shapeBuffer.Length);
            int rows = BitConverter.ToInt32(shapeBuffer, 0);
            int cols = BitConverter.ToInt32(shapeBuffer, 4);

            // Receive the array data
            int arraySize = rows * cols * sizeof(int); // Assuming int32 values
            byte[] arrayBuffer = new byte[arraySize];
            stream.Read(arrayBuffer, 0, arrayBuffer.Length);

            // Convert byte array to int array
            int[,] array = new int[rows, cols];
            Buffer.BlockCopy(arrayBuffer, 0, array, 0, arrayBuffer.Length);

            return array;
        }
    }
}
