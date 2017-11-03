using System;
using System.Threading;
using System.Collections.Generic;

namespace Samples
{
    public class Echoer
    {
        // Suggested input to visualize long-running GC is 4000
        public string Write(string input)
        {
            int size = int.Parse(input);
            List<BigData> bigList = new List<BigData>();
            for (int i = 0; i < size; ++i)
            {
                var elem = new BigData();
                elem.Array[0] = 1;
                bigList.Add(elem);
                Thread.Sleep(10);
            }
            return bigList.Count.ToString();
        }
    }

    public class BigData
    {
        public int[] Array = new int[3000];
    }
}
