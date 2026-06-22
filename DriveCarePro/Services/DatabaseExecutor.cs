using DriveCareCore.Data.BD;
using System;
using System.Data.Entity;

namespace DriveCarePro.Services
{
    /// <summary>Асинхронные запросы в отдельном DbContext (не блокируют UI и не конфликтуют с singleton).</summary>
    internal static class DatabaseExecutor
    {
        public static T WithDb<T>(Func<DriveCareDBEntities, T> work)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            using (var db = new DriveCareDBEntities())
                return work(db);
        }

        public static async System.Threading.Tasks.Task<T> WithDbAsync<T>(Func<DriveCareDBEntities, System.Threading.Tasks.Task<T>> work)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            using (var db = new DriveCareDBEntities())
            {
                return await work(db).ConfigureAwait(false);
            }
        }

        public static System.Threading.Tasks.Task WithDbAsync(Func<DriveCareDBEntities, System.Threading.Tasks.Task> work)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            return WithDbAsync(async db =>
            {
                await work(db).ConfigureAwait(false);
                return 0;
            });
        }
    }
}
