namespace DriveCareCore.Data.BD
{
    public class AppConnect
    {
        public static DriveCareDBEntities model1 = new DriveCareDBEntities();

        /// <summary>Сбрасывает singleton DbContext после операций, которые могли оставить устаревший change tracker.</summary>
        public static void ResetModel()
        {
            try
            {
                model1?.Dispose();
            }
            catch
            {
            }

            model1 = new DriveCareDBEntities();
        }
    }
}
