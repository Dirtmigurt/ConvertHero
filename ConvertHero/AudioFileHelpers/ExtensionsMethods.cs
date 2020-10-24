namespace ConvertHero.AudioFileHelpers
{
    using System.Collections.Generic;

    /// <summary>
    /// List of useful extension methods to reduce code duplication.
    /// </summary>
    public static class ExtensionsMethods
    {
        /// <summary>
        /// Resize a list to the desired size. Use the provided value to fill in any empty space in the new list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="finalSize"></param>
        /// <param name="defaultVal"></param>
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

        /// <summary>
        /// Initialize a 2-Dimensional List.
        /// </summary>
        /// <typeparam name="T">
        /// The type of object held in the list.
        /// </typeparam>
        /// <param name="rows">
        /// The number of rows in the list.
        /// </param>
        /// <param name="columns">
        /// The number of columns in the list.
        /// </param>
        /// <param name="defaultVal">
        /// The value to be filled into every empty slot of the matrix.
        /// </param>
        /// <returns>
        /// The initialized matrix.
        /// </returns>
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
