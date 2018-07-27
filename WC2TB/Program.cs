using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            // Iterate throught layers.
            foreach (XmlElement layer in layers)
            {
                Console.WriteLine("Layer " + layer.GetAttribute("Name") + " exported");

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

                                // Extract object's data.
                                var x = (objectData[off] + TBOffset).ToString(CultureInfo.InvariantCulture);
                                var y = objectData[off + 2].ToString(CultureInfo.InvariantCulture);
                                var z = objectData[off + 1].ToString(CultureInfo.InvariantCulture);
                                var yaw = 0.ToString(CultureInfo.InvariantCulture);
                                var pitch = 0.ToString(CultureInfo.InvariantCulture);
                                var roll = 0.ToString(CultureInfo.InvariantCulture);
                                var scale = objectData[off + 4].ToString(CultureInfo.InvariantCulture);

                                /*
                                var quat = new Quaternion(objectData[off + 5],
                                  objectData[off + 6], objectData[off + 7], objectData[off + 8]);
                                instance.rotation = quat.eulerAngles.y;
                                instance.prototypeIndex = index;
                                terrainComponent.AddTreeInstance(instance);
                                */

                                // model, x, y, yaw, pitch, roll, scale, z
                                string entry = $"\"{tag}\";{x};{y};{yaw};{pitch};{roll};{scale};{z};";

                                file.WriteLine(entry);
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}
