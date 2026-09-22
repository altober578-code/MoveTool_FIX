using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace MoveLib
{
    public static class FileTypeDecider
    {
        public static FileType Decide(string fileName)
        {
            try
            {
                using (var fs = File.OpenRead(fileName))
                {
                    using (var inFile = new BinaryReader(fs))
                    {
                        inFile.BaseStream.Seek(0x18, SeekOrigin.Begin);
                        int OffsetToStart = inFile.ReadInt32();

                        inFile.BaseStream.Seek(OffsetToStart + 0x24, SeekOrigin.Begin);

                        string fileType = new string(inFile.ReadChars(4));

                        Debug.WriteLine("filetype: " + fileType);

                        if (fileType == "#BAC")
                        {
                            return FileType.BAC;
                        }

                        if (fileType == "#BCM")
                        {
                            return FileType.BCM;
                        }

                        if (fileType == "#BCH")
                        {
                            return FileType.BCH;
                        }

                        return FileType.Unknown;
                    }
                }
            }
            catch (Exception ex)
            {
                return FileType.Unknown;
            }

        }

        /// <summary>
        /// Works out which format a json holds by looking at its top level property
        /// names, so a caller can run the one converter that stands a chance instead
        /// of trying each in turn and reporting the failures of the wrong ones.
        /// Only the root properties are read, so a large file costs nothing.
        /// </summary>
        public static FileType DecideJson(string fileName)
        {
            try
            {
                using (var sr = new StreamReader(fileName))
                using (var reader = new JsonTextReader(sr))
                {
                    while (reader.Read())
                    {
                        if (reader.TokenType != JsonToken.PropertyName || reader.Depth != 1)
                        {
                            continue;
                        }

                        switch ((string) reader.Value)
                        {
                            case "MoveLists":
                            case "HitboxEffectses":
                            case "BACVER":
                                return FileType.BAC;

                            case "Charges":
                            case "Inputs":
                            case "CancelLists":
                                return FileType.BCM;

                            case "BCH":
                                return FileType.BCH;
                        }
                    }
                }
            }
            catch (Exception)
            {
                return FileType.Unknown;
            }

            return FileType.Unknown;
        }
    }

    public enum FileType
    {
        Unknown = 0,
        BCM = 1,
        BAC = 2,
        BCH = 3
    }
}
