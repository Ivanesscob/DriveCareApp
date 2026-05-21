using System;
using System.Collections.Generic;

namespace DriveCareCore.Maps
{
    public sealed class WorkshopMapPin
    {
        /// <summary>Основная мастерская для записи / чата (первая в здании).</summary>
        public Guid WorkshopId { get; set; }

        public List<Guid> WorkshopIds { get; set; } = new List<Guid>();
        public List<Guid> BusinessTypeIds { get; set; } = new List<Guid>();
        public Guid? AddressId { get; set; }

        public string WorkshopName { get; set; }
        public string CompanyName { get; set; }
        public string AddressLine { get; set; }
        public string Phone { get; set; }
        public string Description { get; set; }

        /// <summary>Основной тип (совместимость).</summary>
        public Guid? BusinessTypeId { get; set; }
        public string ServiceKindName { get; set; }

        /// <summary>Все типы здания, например «Автосервис · Покраска».</summary>
        public string ServiceKindsLabel { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
