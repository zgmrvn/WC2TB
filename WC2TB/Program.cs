using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace WC2TB
{
    class Program
    {
        private const string Extension = ".txt";
        private const int TBOffset = 200000;

        private static string directory;
        private static int totalObjectsCount = 0;

        #region Main

        static void Main(string[] args)
        {
            // If no file was dragged onto exe, exit.
            if (args.Length == 0)
                return;

            // If it's not an XML file, exit.
            if (Path.GetExtension(args[0]) != ".xml")
                return;

            // Load XML.
            var xml = new XmlDocument();
            xml.Load(Path.GetFileName(args[0]));

            // If it's not a World Creator project, exit.
            if (xml.ChildNodes[0].Name != "WorldCreator")
                return;

            // Get the XML directory for export destination.
            directory = Path.GetDirectoryName(args[0]);

            // Find layers.
            XmlNodeList objectElements = xml.GetElementsByTagName("Objects");
            XmlNode objects = objectElements[0];
            XmlNode layers = objects.ChildNodes[0];

            // Process Layers.
            Layers(in layers);

            Console.WriteLine("Total exported object(s) : {0}.", totalObjectsCount.ToString("N0"));

            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }

        #endregion

        #region Layers

        /// <summary>
        /// Creates a threaded task for each World Creator's layers.
        /// </summary>
        /// <param name="layers"></param>
        private static void Layers(in XmlNode layers)
        {
            Task[] taskArray = new Task[layers.ChildNodes.Count];

            for (int i = 0; i < taskArray.Length; i++)
            {
                var layer = layers.ChildNodes[i] as XmlElement;
                taskArray[i] = Task.Run(delegate { Layer(layer); });
            }

            Task.WaitAll(taskArray);
        }

        #endregion

        #region Layer

        /// <summary>
        /// Creates a file steam and exports layers' objects.
        /// </summary>
        /// <param name="layer"></param>
        private static void Layer(XmlElement layer)
        {
            // Open a new file stream.
            string path = directory + Path.DirectorySeparatorChar + layer.GetAttribute("Name") + Extension;
            var stream = new StreamWriter(path);

            // process objects.
            int layerObjectsCount = Objects(in layer, in stream);

            stream.Close();

            // Display layer's objects count.
            Console.WriteLine(
                "Thread #{0} | Layer {1} exported : {2} object(s).",
                Thread.CurrentThread.ManagedThreadId,
                layer.GetAttribute("Name"),
                layerObjectsCount.ToString("N0")
            );

            totalObjectsCount += layerObjectsCount;
        }

        #endregion

        #region Objects

        /// <summary>
        /// Exports objects of the passed layer to the passed stream in a Terrain Builder compatible format.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="stream"></param>
        private static int Objects(in XmlElement layer, in StreamWriter stream)
        {
            int layerObjectsCount = 0;

            // Iterate throught each object of the current layer.
            foreach (XmlElement obj in layer.ChildNodes)
            {
                string tag = obj.GetAttribute("Tag");

                // Extract encoded objects data.
                string dataString = obj.InnerText;
                string dataCountString = obj.ChildNodes[0].Attributes["DataCount"].Value;

                if (int.TryParse(dataCountString, out int dataCount))
                {
                    const int elementCount = 9;
                    var data = Convert.FromBase64String(dataString);
                    var objectData = new float[dataCount * elementCount];
                    Buffer.BlockCopy(data, 0, objectData, 0, dataCount * elementCount * 4);

                    for (int i = 0; i < dataCount; i++)
                    {
                        int off = i * elementCount;

                        // Extract rotation.
                        var quaternion = new Quaternion(objectData[off + 5], objectData[off + 6], objectData[off + 7], objectData[off + 8]);
                        quaternion = YUpToZUp(quaternion);
                        Vector3 angles = GetTerrainBuilderAngles(quaternion);

                        // Prepare final data.
                        string posX = (objectData[off] + TBOffset).ToString("F");
                        string posY = objectData[off + 2].ToString(CultureInfo.InvariantCulture);
                        string posZ = objectData[off + 1].ToString(CultureInfo.InvariantCulture);
                        string rotX = angles.X.ToString(CultureInfo.InvariantCulture);
                        string rotY = angles.Y.ToString(CultureInfo.InvariantCulture);
                        string rotZ = angles.Z.ToString(CultureInfo.InvariantCulture);
                        string scale = objectData[off + 4].ToString(CultureInfo.InvariantCulture);

                        // yaw (y), pitch (x), roll (z)
                        string entry = $"\"{tag}\";{posX};{posY};{rotZ};{rotX};{rotY};{scale};{posZ};";

                        stream.WriteLine(entry);
                        layerObjectsCount++;
                    }
                }
            }

            return layerObjectsCount;
        }

        #endregion

        #region Y-Up to Z-Up

        /// <summary>
        /// Transforms a Quaternion in Unity coordinate space
        /// to a Quaternion in Arma coordinate space.
        /// </summary>
        /// <param name="rotation"></param>
        /// <returns></returns>
        private static Quaternion YUpToZUp(Quaternion quaternion)
        {
            Matrix4x4 matrix = Matrix4x4.CreateFromQuaternion(quaternion);

            var permutation = new Matrix4x4(
                1, 0, 0, 0,
                0, 0, 1, 0,
                0, 1, 0, 0,
                0, 0, 0, 1
            );

            return Quaternion.CreateFromRotationMatrix(permutation * matrix);
        }

        #endregion

        #region GetTerrainBuilderAngles

        /// <summary>
        /// Takes a Quaternion and returns Terrain Builder compatible angles. 
        /// </summary>
        /// <param name="rotation"></param>
        /// <returns></returns>
        private static Vector3 GetTerrainBuilderAngles(Quaternion quaternion)
        {
            Vector3 dir = Vector3.Transform(Vector3.UnitY, quaternion);
            Vector3 up = Vector3.Transform(Vector3.UnitZ, quaternion);
            Vector3 aside = Vector3.Cross(dir, up);

            double xRot, yRot, zRot;

            if (Math.Abs(up.Y) < 0.999f)
            {
                xRot = -Math.Asin(up.Y);

                double signCosX = (Math.Cos(xRot) < 0f) ? -1 : 1;

                yRot = Math.Atan2(up.X * signCosX, up.Z * signCosX);
                zRot = Math.Atan2(-aside.Y * signCosX, dir.Y * signCosX);
            }

            else
            {
                zRot = 0f;

                if (up.Y < 0f)
                {
                    xRot = Math.PI * 90f / 180f;
                    yRot = Math.Atan2(dir.X, dir.Z);
                }

                else
                {
                    xRot = Math.PI * -90f / 180f;
                    yRot = Math.Atan2(-dir.X, -dir.Z);
                }
            }

            // Rad to deg.
            Vector3 rotation = 180f * new Vector3((float)xRot, (float)yRot, (float)zRot) / (float)Math.PI;

            return rotation;
        }

        #endregion
    }
}
