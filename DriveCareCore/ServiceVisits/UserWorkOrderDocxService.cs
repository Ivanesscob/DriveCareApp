using DriveCareCore.Data.BD;
using DriveCareCore.WorkOrders;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.ServiceVisits
{
    public static class UserWorkOrderDocxService
    {
        public static Task<(bool ok, string error)> TryGenerateAndOpenAsync(Guid userId, Guid documentId) =>
            WithDb(db => TryGenerateAndOpenAsync(db, userId, documentId));

        public static async Task<(bool ok, string error)> TryGenerateAndOpenAsync(
            DriveCareDBEntities db,
            Guid userId,
            Guid documentId)
        {
            if (userId == Guid.Empty || documentId == Guid.Empty)
                return (false, "Некорректные данные.");

            var preview = await UserServiceVisitService.TryLoadWorkOrderAsync(db, userId, documentId)
                .ConfigureAwait(false);
            if (preview == null)
                return (false, "Не удалось загрузить заказ-наряд. Проверьте, что ремонт завершён и документ привязан к вашему аккаунту.");

            var model = await BuildModelAsync(db, userId, documentId, preview).ConfigureAwait(false);
            if (model == null)
                return (false, "Не удалось собрать данные для заказ-наряда.");

            return await Task.Run(() =>
            {
                var path = RepairWorkOrderPrintService.GetDesktopOrderPath("Zakaz-naryad");
                var (genOk, savedPath, genError) = RepairWorkOrderPrintService.TryGenerateFilled(model, path);
                if (!genOk)
                    return (false, genError ?? "Не удалось сформировать документ Word.");

                if (RepairWorkOrderPrintService.TryOpenDocument(savedPath, out var openError))
                    return (true, null);

                return (false, openError ?? ("Документ сохранён, но не удалось открыть:\n" + savedPath));
            }).ConfigureAwait(false);
        }

        static async Task<RepairWorkOrderModel> BuildModelAsync(
            DriveCareDBEntities db,
            Guid userId,
            Guid documentId,
            UserWorkOrderPreview preview)
        {
            var doc = await db.ServiceDocuments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.RowId == documentId && d.ClientUserId == userId)
                .ConfigureAwait(false);
            if (doc == null)
                return null;

            var model = new RepairWorkOrderModel
            {
                VisitReason = preview.VisitReason ?? string.Empty,
                RepairType = string.IsNullOrWhiteSpace(preview.ServiceKind) ? "Ремонт" : preview.ServiceKind.Trim(),
                SpecialNotes = preview.ReportText ?? string.Empty
            };

            var completedAt = preview.CompletedAt ?? doc.CompletedAt ?? DateTime.Now;
            model.OrderDate = completedAt.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));
            model.OrderTime = completedAt.ToString("HH:mm", CultureInfo.GetCultureInfo("ru-RU"));

            if (preview.MileageKm.HasValue && preview.MileageKm.Value > 0)
                model.Mileage = preview.MileageKm.Value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));

            var workshop = await db.Workshops.AsNoTracking()
                .FirstOrDefaultAsync(w => w.RowId == doc.WorkshopId)
                .ConfigureAwait(false);
            if (workshop != null)
            {
                model.CompanyName = workshop.Name?.Trim() ?? preview.WorkshopName ?? string.Empty;
                if (workshop.CompanyId != Guid.Empty)
                {
                    var company = await db.Companies.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.RowId == workshop.CompanyId)
                        .ConfigureAwait(false);
                    if (company != null && !string.IsNullOrWhiteSpace(company.Name))
                        model.CompanyName = company.Name.Trim();
                }

                if (workshop.AddressId.HasValue)
                {
                    var addr = await db.Addresses.AsNoTracking()
                        .FirstOrDefaultAsync(a => a.RowId == workshop.AddressId.Value)
                        .ConfigureAwait(false);
                    if (addr != null)
                    {
                        model.CompanyLegalAddress = !string.IsNullOrWhiteSpace(addr.FullAddress)
                            ? addr.FullAddress.Trim()
                            : string.Join(", ", new[] { addr.City, addr.Street, addr.House, addr.Apartment }
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Select(s => s.Trim()));
                    }
                }
            }
            else
            {
                model.CompanyName = preview.WorkshopName ?? string.Empty;
            }

            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.RowId == userId).ConfigureAwait(false);
            if (user != null)
            {
                model.ClientName = FirstNonEmpty(user.Description, user.Login, user.Email) ?? string.Empty;
                model.ClientPhone = user.Phone?.Trim() ?? string.Empty;
                model.ClientAddress = user.Email?.Trim() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(model.ClientName))
                model.ClientName = doc.ClientName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model.ClientPhone))
                model.ClientPhone = doc.ClientPhone?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model.ClientAddress))
                model.ClientAddress = doc.ClientEmail?.Trim() ?? string.Empty;

            if (doc.CarId.HasValue && doc.CarId.Value != Guid.Empty)
            {
                var car = await db.Cars.AsNoTracking()
                    .Include(c => c.Model)
                    .Include(c => c.Model.Brand)
                    .FirstOrDefaultAsync(c => c.RowId == doc.CarId.Value)
                    .ConfigureAwait(false);
                if (car != null)
                {
                    var brand = car.Model?.Brand?.Name?.Trim();
                    var carModel = car.Model?.Name?.Trim();
                    model.CarDescription = string.Join(" ", new[] { brand, carModel }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    model.Year = car.Year?.ToString() ?? string.Empty;
                    model.Vin = car.Vin?.Trim() ?? string.Empty;
                    model.PlateNumber = car.PlateNumber?.Trim() ?? string.Empty;
                }
            }

            model.WorkLines = ToWorkLines(preview.Services);
            model.PartLines = ToPartLines(preview.Parts);
            model.RecalculateTotals();
            return model;
        }

        static List<RepairWorkOrderWorkLine> ToWorkLines(IEnumerable<UserWorkOrderLineVm> lines) =>
            (lines ?? Enumerable.Empty<UserWorkOrderLineVm>())
                .Where(l => !string.IsNullOrWhiteSpace(l?.Name))
                .Select((l, i) => new RepairWorkOrderWorkLine
                {
                    Code = (i + 1).ToString(),
                    Name = l.Name.Trim(),
                    Multiplicity = l.Quantity.ToString("0.###", CultureInfo.InvariantCulture),
                    PricePerHour = l.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture),
                    Amount = l.LineAmount.ToString("0.00", CultureInfo.InvariantCulture),
                    Cost = l.LineAmount.ToString("0.00", CultureInfo.InvariantCulture),
                    Discount = l.DiscountPercent > 0
                        ? l.DiscountPercent.ToString("0.##", CultureInfo.InvariantCulture)
                        : string.Empty
                })
                .ToList();

        static List<RepairWorkOrderPartLine> ToPartLines(IEnumerable<UserWorkOrderLineVm> lines) =>
            (lines ?? Enumerable.Empty<UserWorkOrderLineVm>())
                .Where(l => !string.IsNullOrWhiteSpace(l?.Name))
                .Select((l, i) => new RepairWorkOrderPartLine
                {
                    Number = (i + 1).ToString(),
                    Name = l.Name.Trim(),
                    Unit = string.IsNullOrWhiteSpace(l.UnitName) ? "шт." : l.UnitName.Trim(),
                    Quantity = l.Quantity.ToString("0.###", CultureInfo.InvariantCulture),
                    Price = l.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture),
                    Amount = l.LineAmount.ToString("0.00", CultureInfo.InvariantCulture)
                })
                .ToList();

        static string FirstNonEmpty(params string[] values) =>
            values?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

        static async Task<T> WithDb<T>(Func<DriveCareDBEntities, Task<T>> action)
        {
            using (var db = new DriveCareDBEntities())
                return await action(db).ConfigureAwait(false);
        }
    }
}
