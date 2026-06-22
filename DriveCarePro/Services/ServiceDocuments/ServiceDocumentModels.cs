using DriveCareCore.ServiceVisits;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DriveCarePro.Services.ServiceDocuments
{
    public enum ServiceDocumentStatus : byte
    {
        Open = 0,
        Completed = 1
    }

    public sealed class ServiceDocumentInfo
    {
        public Guid DocumentId { get; set; }
        public Guid RootTaskId { get; set; }
        public ServiceDocumentStatus Status { get; set; }
        public string Title { get; set; }
        public string StatusDisplay { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsCurrentTaskRoot { get; set; }
        public ServiceDocumentClientStage ClientStage { get; set; }
        public string ClientStageDisplay { get; set; }
    }

    public sealed class ServiceDocumentPreview
    {
        public ServiceDocumentInfo Info { get; set; }
        public string ReportText { get; set; } = string.Empty;
        public List<WorkshopServices.TaskServiceLineRow> Services { get; set; } =
            new List<WorkshopServices.TaskServiceLineRow>();
        public List<WorkshopServices.TaskPartLineRow> Parts { get; set; } =
            new List<WorkshopServices.TaskPartLineRow>();
    }

    public sealed class TaskTreeNodeVm
    {
        public Guid TaskId { get; set; }
        /// <summary>Номер в дереве: 1, 1.1, 2.1.1 …</summary>
        public string LevelNumber { get; set; } = string.Empty;
        public string Title { get; set; }
        public string EmployeeName { get; set; }
        public string StatusDisplay { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsCurrentEmployeeTask { get; set; }
        /// <summary>Это задание открыто в карточке (подсветка в дереве).</summary>
        public bool IsCurrentTask { get; set; }
        public bool IsRoot { get; set; }
        public int Depth { get; set; }
        /// <summary>Все дочерние в дереве завершены — можно подсветить своё задание.</summary>
        public bool IsReadyToComplete { get; set; }

        public ObservableCollection<TaskTreeNodeVm> Children { get; } = new ObservableCollection<TaskTreeNodeVm>();
    }
}
