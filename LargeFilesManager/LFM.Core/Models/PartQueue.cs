using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LFM.Core.Models
{
    public class PartQueue
    {
        public int Index { get; set; }

        public List<string> Lines { get; set; }

        public PartQueue(int index, List<string> lines)
        {
            Index = index;
            Lines = lines;
        }

        // Free memory
        public void ClearLines()
        {
            Lines.Clear();
            Lines = null!;
        }
    }
}
