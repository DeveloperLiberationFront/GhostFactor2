﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WarningTest
{
    public class TestUtil
    {

        public static String generateRandomString(int size)
        {
            var random = new Random((int)DateTime.Now.Ticks);
            var builder = new StringBuilder();
            char ch;
            for (int i = 0; i < size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                builder.Append(ch);
            }

            return builder.ToString();
        }

        public static int generateRandomInt(int low, int high)
        {
            var random = new Random((int) DateTime.Now.Ticks);
            return random.Next(low, high);
        }

        public static string GetTestProjectPath()
        {
            return @"C:\programming\VS Workspace\ghostfactor\WarningTest\";
        }

        public static string GetTestProjectFilePath()
        {
            return @"C:\programming\Vs Workspace\ghostfactor\WarningTest\WarningTest.csproj";
        }

        public static string GetFakeSourceFolder()
        {
            return @"C:\programming\VS Workspace\ghostfactor\WarningTest\fakesource\";
        }

        public static string GetSolutionPath()
        {
            return @"C:\programming\VS Workspace\ghostfactor\warnings.sln";
        }

        public static string GetAnotherSolutionPath()
        {
            return @"C:\programming\VS Workspace\BeneWarShadow\CodeIssue1\CodeIssue1.sln";
        }
    }
}
