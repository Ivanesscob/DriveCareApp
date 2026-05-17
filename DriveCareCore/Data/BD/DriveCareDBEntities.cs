namespace DriveCareCore.Data.BD
{
    /// <summary>Совместимость: приложение использует DriveCareDBEntities, EDMX генерирует DriveCareDBEntities2.</summary>
    public class DriveCareDBEntities : DriveCareDBEntities2
    {
        public DriveCareDBEntities()
            : base("name=DriveCareDBEntities")
        {
        }
    }
}
