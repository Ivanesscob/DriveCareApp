using System;

namespace DriveCareCore.ServiceVisits
{
    /// <summary>Этап ремонта для клиента (ServiceDocuments.ClientStage).</summary>
    public enum ServiceDocumentClientStage : byte
    {
        Unknown = 0,
        Accepted = 1,
        InRepair = 2,
        ReadyForPickup = 3,
        Completed = 4
    }

    public static class ServiceDocumentClientStageLabels
    {
        public const string Accepted = "Машина принята";
        public const string InRepair = "Машина в ремонте";
        public const string ReadyForPickup = "Машина готова к выдаче";
        public const string Completed = "Ремонт завершён";

        public static string ForUser(ServiceDocumentClientStage stage)
        {
            switch (stage)
            {
                case ServiceDocumentClientStage.Accepted: return Accepted;
                case ServiceDocumentClientStage.InRepair: return InRepair;
                case ServiceDocumentClientStage.ReadyForPickup: return ReadyForPickup;
                case ServiceDocumentClientStage.Completed: return Completed;
                default: return InRepair;
            }
        }

        public static string ForUser(byte stage) => ForUser((ServiceDocumentClientStage)stage);

        public static int StepIndex(ServiceDocumentClientStage stage)
        {
            switch (stage)
            {
                case ServiceDocumentClientStage.Accepted: return 1;
                case ServiceDocumentClientStage.InRepair: return 2;
                case ServiceDocumentClientStage.ReadyForPickup: return 3;
                default: return 0;
            }
        }

        public static ServiceDocumentClientStage Normalize(byte raw, byte documentStatus)
        {
            if (documentStatus == 1)
                return ServiceDocumentClientStage.Completed;

            if (raw >= (byte)ServiceDocumentClientStage.Accepted
                && raw <= (byte)ServiceDocumentClientStage.Completed)
                return (ServiceDocumentClientStage)raw;

            return documentStatus == 0
                ? ServiceDocumentClientStage.InRepair
                : ServiceDocumentClientStage.Completed;
        }
    }
}
