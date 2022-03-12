using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XLinkLibrary
{
    public class BitFlag
    {
        static int CountOnBit(uint x)
        {
            x = x - ((x >> 1) & 0x55555555);
            x = (x & 0x33333333) + ((x >> 2) & 0x33333333);
            x = (x + (x >> 4)) & 0x0F0F0F0F;
            x += (x >> 8);
            x += (x >> 16);
            return (int)x & 0x3f;
        }

        public static int CountRightOnBit(uint x, int bit)
        {
            uint mask = ((1u << bit) - 1) | (1u << bit);
            return CountOnBit(x & mask);
        }

        public static int CountRightOnBit64(ulong x, int bit)
        {
            ulong mask = ((1ul << bit) -1) | (1ul << bit);
            return countOnBit64(x & mask);
        }

        static int countOnBit64(ulong x)
        {
            return CountOnBit((uint)x) + CountOnBit((uint)(x >> 32));
        }
    }
}
