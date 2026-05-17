using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;

namespace DriveCarePro.Services.ServiceBooking
{
    public enum ServiceBookingKind
    {
        Repair,
        Painting
    }

    public enum ServiceClientPath
    {
        Unknown,
        ExistingUserWithSelectedCar,
        ExistingUserWithNewCar,
        ExistingUserGuestCar,
        NewUserRegistered,
        ManualGuest
    }

    public sealed class ServiceBookingContext
    {
        public ServiceBookingKind Kind { get; set; }
        public OwnerOrganizationScope Scope { get; set; }
        public Guid WorkshopId { get; set; }

        public string SearchEmail { get; set; } = string.Empty;
        public string SearchPhone { get; set; } = string.Empty;

        public User FoundUser { get; set; }
        public List<UserCarOption> UserCars { get; set; } = new List<UserCarOption>();

        public ServiceClientPath ClientPath { get; set; } = ServiceClientPath.Unknown;

        public Guid? SelectedCarId { get; set; }
        public Guid? SelectedUserCarId { get; set; }
        public string SelectedCarDisplay { get; set; }

        public string ClientFullName { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientAddress { get; set; } = string.Empty;

        public string CarDescription { get; set; } = string.Empty;
        public string Vin { get; set; } = string.Empty;
        public string PlateNumber { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Mileage { get; set; } = string.Empty;

        public string VisitReason { get; set; } = string.Empty;
        public string SpecialNotes { get; set; } = string.Empty;

        public Guid? CreatedServiceClientId { get; set; }
        public Guid? CreatedRepairHistoryId { get; set; }
        public Guid? CreatedTaskId { get; set; }

        public static ServiceBookingContext Create(ServiceBookingKind kind) =>
            new ServiceBookingContext { Kind = kind };

        public string RepairTypeDisplay => Kind == ServiceBookingKind.Painting ? "Покраска" : "Ремонт";
    }

    public sealed class UserCarOption
    {
        public Guid CarId { get; set; }
        public Guid UserCarId { get; set; }
        public string DisplayName { get; set; }
        public string Vin { get; set; }
        public string PlateNumber { get; set; }
        public int? Year { get; set; }
        public string Color { get; set; }
    }
}
