using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.CrestronXml.Serialization;

namespace Example.Fusion
{
	public static class GuidManager
	{
		/// <summary>
		/// Method to get/create a GUID for general use. If a matching .guid file is found, it reads the contents, if not a new GUID is created and return. This new GUID is then written to disk for future recalling by this method.
		/// </summary>
		/// <param name="ObjectName">Unique object name for which this GUID will be used.<example>example.</example></param>
		/// <returns>Unique GUID.</returns>
		/// <exception cref="ArgumentNullException"></exception>
		public static string GetGuid(string ObjectName)
		{
			// ensure the parameter contains a valid string
			if (string.IsNullOrEmpty(ObjectName))
				throw new ArgumentNullException("Object name cannot be null or an empty string");

			// string object to return
			string guid = string.Empty;

			// pathway to guid directory
			string dir = string.Format("{0}\\GUIDS", Directory.GetApplicationDirectory());

			// fully qualified pathway name
			string fullPath = string.Format("{0}\\{1}.guid", dir, ObjectName);

			// check to see if the directory exists already
			if (Directory.Exists(dir))
			{
				// check to see if the file name exists
				if (File.Exists(fullPath))
				{
					// deserialize object
					guid = CrestronXMLSerialization.DeSerializeObject<string>(fullPath);
				}
				else
				{
					// get new guid and write to disk
					guid = WriteNewGuid(fullPath);
				}
			}
			else
			{
				// create the directory
				Directory.Create(dir);

				// get new guid and write to disk
				guid = WriteNewGuid(fullPath);
			}

			return guid;
		}

		private static string WriteNewGuid(string pathway)
		{
			// create new guid
			string tempGuid = Guid.NewGuid().ToString();

			// write it to disk
			CrestronXMLSerialization.SerializeObject(pathway, tempGuid);

			// return it to calling method
			return tempGuid;
		}
	}
}
