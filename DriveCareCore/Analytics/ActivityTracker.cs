using System;

namespace DriveCareCore.Analytics
{
    /// <summary>Удобные методы записи событий (не блокируют UI).</summary>
    public static class ActivityTracker
    {
        public static void TrackUser(
            string eventCode,
            Guid? userId = null,
            string entityType = null,
            Guid? entityId = null,
            Guid? workshopId = null,
            Guid? companyId = null,
            string payloadJson = null)
        {
            ActivityEventService.LogFireAndForget(new ActivityEventRequest
            {
                EventCode = eventCode,
                ActorKind = ActivityActorKind.User,
                UserId = userId,
                WorkshopId = workshopId,
                CompanyId = companyId,
                EntityType = entityType,
                EntityId = entityId,
                PayloadJson = payloadJson
            });
        }

        public static void TrackEmployee(
            string eventCode,
            Guid? employeeId = null,
            Guid? workshopId = null,
            Guid? companyId = null,
            string entityType = null,
            Guid? entityId = null,
            string payloadJson = null)
        {
            ActivityEventService.LogFireAndForget(new ActivityEventRequest
            {
                EventCode = eventCode,
                ActorKind = ActivityActorKind.Employee,
                EmployeeId = employeeId,
                WorkshopId = workshopId,
                CompanyId = companyId,
                EntityType = entityType,
                EntityId = entityId,
                PayloadJson = payloadJson
            });
        }

        public static void TrackSystem(
            string eventCode,
            string entityType = null,
            Guid? entityId = null,
            Guid? workshopId = null,
            Guid? companyId = null,
            string payloadJson = null)
        {
            ActivityEventService.LogFireAndForget(new ActivityEventRequest
            {
                EventCode = eventCode,
                ActorKind = ActivityActorKind.System,
                WorkshopId = workshopId,
                CompanyId = companyId,
                EntityType = entityType,
                EntityId = entityId,
                PayloadJson = payloadJson
            });
        }
    }
}
