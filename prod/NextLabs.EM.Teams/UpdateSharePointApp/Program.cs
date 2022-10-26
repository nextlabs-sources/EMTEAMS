using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.IO.Compression;

namespace UpdateSharePointApp
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Invalid parameters");
                return;
            }

            //get argument
            string strSharePointApp = args[0];
            string strSetParamFile = args[1];
            if (!File.Exists(strSharePointApp))
            {
                Console.WriteLine("{0} not exist.", strSharePointApp);
                return;
            }

            if (!File.Exists(strSetParamFile))
            {
                Console.WriteLine("{0} not exist.", strSetParamFile);
                return;
            }

            //read para from file
            string strClientID, strRemoteAppUrl;
            bool bRes = GetParameters(strSetParamFile, out strClientID, out strRemoteAppUrl);
            if (!bRes)
            {
                Console.WriteLine("Read parameter failed.");
                return;
            }
            Console.WriteLine("ClientID:{0}", strClientID);
            Console.WriteLine("RemoteUrl:{0}", strRemoteAppUrl);

            //replace 
            bRes = UpdateApp(strSharePointApp, strClientID, strRemoteAppUrl);
            Console.WriteLine("Update SharePoint Application {0}.", bRes ? "success" : "failed");
            Console.ReadKey();
        }

        static bool UpdateApp(string strApp, string strClientID, string strRemoteAppUrl)
        {
            bool result = true;
            try
            {
                //backup 
                string strBackupFile = strApp + ".backup";
                if (File.Exists(strBackupFile))
                {
                    File.Delete(strBackupFile);
                }
                File.Copy(strApp, strApp + ".backup");

                //update
                string strZipPath = strApp;
                using (FileStream strZipFile = new FileStream(strZipPath, FileMode.Open))
                {
                    using (ZipArchive archive = new ZipArchive(strZipFile, ZipArchiveMode.Update))
                    {
                        foreach (ZipArchiveEntry zipEntry in archive.Entries)
                        {
                            string strZipEntryName = zipEntry.Name;
                            if (strZipEntryName.Equals("AppManifest.xml") ||
                                (strZipEntryName.EndsWith(".xml") && strZipEntryName.StartsWith("elements")))
                            {

                                using (System.IO.Stream stream = zipEntry.Open())
                                {
                                    //read entry content
                                    TextReader textReader = new StreamReader(stream);
                                    string strContent = textReader.ReadToEnd();

                                    //update clientID in AppManifest.xml
                                    if (strZipEntryName.Equals("AppManifest.xml"))
                                    {

                                        XmlDocument xmlfile = new XmlDocument();
                                        xmlfile.LoadXml(strContent);
                                        var RemoteWebApplication = xmlfile.GetElementsByTagName("RemoteWebApplication");
                                        if (RemoteWebApplication != null)
                                        {
                                            RemoteWebApplication[0].Attributes["ClientId"].InnerText = strClientID;
                                            Console.WriteLine("Replace ClientID to: {0} success!", strZipEntryName);

                                            StringWriter sw = new StringWriter();
                                            XmlTextWriter xmlTextWriter = new XmlTextWriter(sw);
                                            xmlfile.WriteTo(xmlTextWriter);

                                            strContent = sw.ToString();
                                        }
                                    }

                                    //update remoteAppUrl
                                    strContent = strContent.Replace("~remoteAppUrl", strRemoteAppUrl);

                                    //write to stream
                                    stream.Position = 0;
                                    stream.SetLength(0);
                                    byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(strContent);
                                    stream.Write(byteArray, 0, byteArray.Length);

                                    stream.Flush();
                                    stream.Close();

                                }
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Update SharePoint application failed:{0}", ex.ToString());
                result = false;
            }


            Console.WriteLine("Update SharePoint application success.");
            return result;
        }


        static bool GetParameters(string strSetParamFile, out string strClientID, out string strRemoteAppUrl)
        {
            strClientID = "";
            strRemoteAppUrl = "";
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(strSetParamFile);

                XmlNodeList lstNodeSetPara = xmlDoc.SelectNodes("parameters/setParameter");
                foreach (XmlNode nodeSetPara in lstNodeSetPara)
                {
                    if (nodeSetPara.Attributes["name"].Value == "ClientId")
                    {
                        strClientID = nodeSetPara.Attributes["value"].Value;
                    }
                    else if (nodeSetPara.Attributes["name"].Value == "remoteAppUrl")
                    {
                        strRemoteAppUrl = nodeSetPara.Attributes["value"].Value;
                    }
                }

            }
            catch (System.Exception ex)
            {
                Console.Write("Exception happened when read SetParaFile:{0}", ex.ToString());
                return false;
            }

            return (!string.IsNullOrWhiteSpace(strClientID)) &&
                    (!string.IsNullOrWhiteSpace(strRemoteAppUrl));
        }
    }
}
