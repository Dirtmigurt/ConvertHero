using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public static class ExtensionsMethods
    {
        public static void Resize<T>(this List<T> list, int finalSize, T defaultVal = default(T)) where T : struct
        {
            while(list.Count < finalSize)
            {
                list.Add(defaultVal);
            }
            
            while(list.Count > finalSize)
            {
                list.RemoveAt(list.Count - 1);
            }
        }

        public static List<List<T>> New2DList<T>(int rows, int columns, T defaultVal = default(T))
        {
            List<List<T>> list = new List<List<T>>(rows);
            for(int i = 0; i < rows; i++)
            {
                list.Add(new List<T>(columns));
                for(int j = 0; j < columns; j++)
                {
                    list[i].Add(defaultVal);
                }
            }

            return list;
        }
    }
}
