using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Reflection;
using SPP4;
using System.Diagnostics;
using System.IO;

namespace LoggerTest
{
    [TestClass]
    public class LoggerUnitTest
    {
        [TestMethod]
        public void EqualLogs()
        {
            string dirPath = "Y:\\Учеба-5\\СПП\\4\\SPP4\\SPP4\\SPP4\\bin\\Debug\\";
            string log = "CLASS: {IgorClass}. METHOD: {.ctor}. PARAMETERS: { magic=69 }";
            string path = dirPath + "AwesomeIgor.exe";

            Logger.LoggerAttribute assemblyModifier = new Logger.LoggerAttribute("");
            assemblyModifier.InjectToAssembly(path);

            Process igorProcess = new Process();
            igorProcess.StartInfo = new ProcessStartInfo(path);
            igorProcess.Start();
            igorProcess.WaitForExit();

            string writtenLog;
            using (var fileStream = new FileStream(dirPath + "logger.txt", FileMode.Open, FileAccess.Read))
            using (var streamReader = new StreamReader(fileStream))
                writtenLog = streamReader.ReadLine();

            Assert.AreEqual(log, writtenLog);
        }
    }
}
