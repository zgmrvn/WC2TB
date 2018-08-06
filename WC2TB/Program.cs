using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Xml;

namespace WC2TB
{
    class Program
    {
        private const string Extension = ".txt";
        private const int TBOffset = 200000;

        private static string Directory;

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
            Directory = Path.GetDirectoryName(args[0]);

            // Process objects data.
            ExportObjects(ref xml);

            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }

        #endregion

        #region Export objects

        /**
         * Processes World Creator's projet and exports data
         * in a Terrain Builder compatible format.
         */
        private static void ExportObjects(ref XmlDocument xml)
        {
            // Find layers.
            XmlNodeList objectElements = xml.GetElementsByTagName("Objects");
            XmlNode objects = objectElements[0];
            XmlNode layers = objects.ChildNodes[0];

            // objects count
            int totalObjectsCount = 0;
            int layerObjectsCount = 0;

            // Iterate throught layers.
            foreach (XmlElement layer in layers)
            {
                // Open a file stream per World Creator layer.
                string path = Directory + Path.DirectorySeparatorChar + layer.GetAttribute("Name") + Extension;

                using (StreamWriter file = new StreamWriter(path))
                {
                    // Iterate throught each object of the current layer.
                    foreach (XmlElement obj in layer.ChildNodes)
                    {
                        //string objName = obj.GetAttribute("Name");
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
                                Vector3 euler = quaternion.Euler();

                                // Prepare final data.
                                string posX = (objectData[off] + TBOffset).ToString(CultureInfo.InvariantCulture);
                                string posY = objectData[off + 2].ToString(CultureInfo.InvariantCulture);
                                string posZ = objectData[off + 1].ToString(CultureInfo.InvariantCulture);
                                string rotY = euler.Y.ToString(CultureInfo.InvariantCulture);
                                string rotX = euler.X.ToString(CultureInfo.InvariantCulture);
                                string rotZ = euler.Z.ToString(CultureInfo.InvariantCulture);
                                string scale = objectData[off + 4].ToString(CultureInfo.InvariantCulture);

                                // model, x, y, yaw (x), pitch (y), roll (z), scale, z
                                string entry = $"\"{tag}\";{posX};{posY};{rotY};{rotX};{rotZ};{scale};{posZ};";

                                file.WriteLine(entry);
                                layerObjectsCount++;
                            }
                        }
                    }
                }

                // Display layer's objects count.
                Console.WriteLine("Layer " + layer.GetAttribute("Name") + " exported : " + layerObjectsCount + " object(s).");
                totalObjectsCount += layerObjectsCount;
                layerObjectsCount = 0;
            }

            // Display total objects count.
            Console.WriteLine("Total exported object(s) : " + totalObjectsCount + ".");
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
