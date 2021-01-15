using Elskom.Generic.Libs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OriginalSOTestCase
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void WithSystemGzipStream()
        {
            const string sample = "This is a compression test of microsoft .net gzip compression method and decompression methods";
            var encoding = new ASCIIEncoding();
            var data = encoding.GetBytes(sample);
            string sampleOut = null;
            byte[] cmpData;

            // Compress
            using (var cmpStream = new MemoryStream())
            {
                using (var hgs = new GZipStream(cmpStream, CompressionMode.Compress))
                {
                    hgs.Write(data, 0, data.Length);
                }
                cmpData = cmpStream.ToArray();
            }

            int corruptBytesNotDetected = 0;

            // corrupt data byte by byte
            for (var byteToCorrupt = 0; byteToCorrupt < cmpData.Length; byteToCorrupt++)
            {
                // corrupt the data
                cmpData[byteToCorrupt]++;

                using (var decomStream = new MemoryStream(cmpData))
                {
                    using (var hgs = new GZipStream(decomStream, CompressionMode.Decompress))
                    {
                        using (var reader = new StreamReader(hgs))
                        {
                            try
                            {
                                sampleOut = reader.ReadToEnd();

                                // if we get here, the corrupt data was not detected by GZipStream
                                // ... okay so long as the correct data is extracted
                                corruptBytesNotDetected++;

                                var message = string.Format("ByteCorrupted = {0}, CorruptBytesNotDetected = {1}",
                                   byteToCorrupt, corruptBytesNotDetected);

                                Debug.WriteLine(message);
                                //Assert.IsNotNull(sampleOut, message);
                                //Assert.AreEqual(sample, sampleOut, message);
                            }
                            catch (InvalidDataException)
                            {
                                var message = string.Format("ByteCorrupted = {0}, CorruptBytesProperlyDetected = {1}",
                                   byteToCorrupt, corruptBytesNotDetected);

                                Debug.WriteLine(message);
                                // data was corrupted, so we expect to get here
                            }
                        }
                    }
                }

                // restore the data
                cmpData[byteToCorrupt]--;
            }
        }

        [TestMethod]
        public void WithManagedZlibImpl()
        {
            const string sample = "This is a compression test of microsoft .net gzip compression method and decompression methods";
            var encoding = new ASCIIEncoding();
            var data = encoding.GetBytes(sample);
            string sampleOut = null;
            byte[] cmpData;

            // Compress
            using (var cmpStream = new MemoryStream())
            {
                using (var hgs = new ZOutputStream(cmpStream, ZlibCompression.ZBESTSPEED))
                {
                    hgs.Write(data, 0, data.Length);
                }
                cmpData = cmpStream.ToArray();
            }

            int corruptBytesNotDetected = 0;

            // corrupt data byte by byte
            for (var byteToCorrupt = 0; byteToCorrupt < cmpData.Length; byteToCorrupt++)
            {
                // corrupt the data
                cmpData[byteToCorrupt]++;

                using (var decomStream = new MemoryStream(cmpData))
                {
                    using (var hgs = new ZInputStream(decomStream))
                    {
                        using (var reader = new StreamReader(hgs))
                        {
                            try
                            {
                                sampleOut = reader.ReadToEnd();

                                // if we get here, the corrupt data was not detected by GZipStream
                                // ... okay so long as the correct data is extracted
                                corruptBytesNotDetected++;

                                var message = string.Format("ByteCorrupted = {0}, CorruptBytesNotDetected = {1}",
                                   byteToCorrupt, corruptBytesNotDetected);

                                Debug.WriteLine(message);
                                //Assert.IsNotNull(sampleOut, message);
                                //Assert.AreEqual(sample, sampleOut, message);
                            }
                            catch (InvalidDataException)
                            {
                                // data was corrupted, so we expect to get here
                            }
                            catch(ZStreamException ex)
                            {
                                Debug.WriteLine("ZStreamException caught");
                                Debug.WriteLine(ex.Message);
                            }
                        }
                    }
                }

                // restore the data
                cmpData[byteToCorrupt]--;
            }
        }
    }
}
