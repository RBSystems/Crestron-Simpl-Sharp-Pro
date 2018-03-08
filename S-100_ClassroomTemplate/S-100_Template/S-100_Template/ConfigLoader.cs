using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.CrestronXml.Serialization;

namespace S_100_Template
{
    public class ConfigLoader<T> where T : IConfigData
    {
        public string FileName { get; private set; }
        public string LoadError { get; private set; }
        public bool IsLoaded { get; private set; }

        public T Details;

        public ConfigLoader(string paramFilePathway)
        {
            CrestronConsole.PrintLine("trying to load {0}", paramFilePathway);

            if (string.IsNullOrEmpty(paramFilePathway))
                throw new ArgumentNullException("Must pass a valid file pathway parameter");

            if (File.Exists(paramFilePathway))
            {
                this.FileName = paramFilePathway;

                try
                {
                    this.Details = CrestronXMLSerialization.DeSerializeObject<T>(paramFilePathway);
                    this.IsLoaded = true;
                    CrestronConsole.PrintLine("Yo the file loaded");
                }
                catch (Exception e)
                {
                    this.LoadError = string.Format("Could not load '{0}'. Cause: {1}", paramFilePathway, e.Message);
                    CrestronConsole.PrintLine("Yo the file {0} did not loaded because {1}", paramFilePathway, e.Message);
                }
            }
            else
            {
                this.LoadError = string.Format("File '{0}' does not exist.", paramFilePathway);
                CrestronConsole.PrintLine("Yo the file aint loaded");
            }
        }

        public ConfigLoader(T paramConfig)
        {
            if (paramConfig == null)
                throw new ArgumentNullException("Must pass a valid IConfigData parameter");

            this.FileName = "\\User\\config.dat";

            this.Details = paramConfig;
            this.IsLoaded = true;
        }

        public void Save()
        {
            if (Details.Modified)
            {
                try
                {
                    CrestronXMLSerialization.SerializeObject(this.FileName, this.Details);

                    Details.Modified = false;

                    CrestronConsole.PrintLine("Saved '{0}' successfully", this.FileName);
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Could not save '{0}'. Cause: {1}", this.FileName, e.Message);
                    ErrorLog.Error("Could not save '{0}'. Cause: {1}", this.FileName, e.Message);
                }
            }
            else
            {
                CrestronConsole.PrintLine("Config unchanged. No need to save.");
            }
        }
    }
}