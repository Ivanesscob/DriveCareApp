using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace DriveCareCore.Maps
{
    public static class YandexWorkshopMapHtmlBuilder
    {
        sealed class MapPinDto
        {
            public string WorkshopId { get; set; }
            public string WorkshopName { get; set; }
            public string CompanyName { get; set; }
            public string AddressLine { get; set; }
            public string Phone { get; set; }
            public string ServiceKindName { get; set; }
            public int ServiceKindCode { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public string IconPreset { get; set; }
        }

        public static string Build(IReadOnlyList<WorkshopMapPin> pins, string apiKey)
        {
            var dtos = (pins ?? new List<WorkshopMapPin>()).Select(p =>
            {
                var code = WorkshopServiceKinds.ResolveCode(p.BusinessTypeId, p.ServiceKindName);
                return new MapPinDto
                {
                    WorkshopId = p.WorkshopId.ToString("D"),
                    WorkshopName = p.WorkshopName,
                    CompanyName = p.CompanyName,
                    AddressLine = p.AddressLine,
                    Phone = p.Phone,
                    ServiceKindName = WorkshopServiceKinds.GetDisplayName(p.BusinessTypeId, p.ServiceKindName),
                    ServiceKindCode = (int)code,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    IconPreset = WorkshopServiceKinds.GetYandexIconPreset(code)
                };
            }).ToList();

            var pinsJson = JsonConvert.SerializeObject(dtos);
            var lat = YandexMapsConfig.DefaultCenterLat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lon = YandexMapsConfig.DefaultCenterLon.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head>");
            sb.AppendLine("<meta charset=\"utf-8\"/>");
            sb.AppendLine("<style>html,body,#map{margin:0;padding:0;width:100%;height:100%;}</style>");
            sb.AppendLine("<script src=\"https://api-maps.yandex.ru/2.1/?apikey=" + HttpUtility.HtmlEncode(apiKey) + "&amp;lang=ru_RU\"></script>");
            sb.AppendLine("</head><body><div id=\"map\"></div><script>");
            sb.AppendLine("var pins = " + pinsJson + ";");
            sb.AppendLine("var placemarks = {};");
            sb.AppendLine(@"
function notifyHost(workshopId) {
    var msg = JSON.stringify({ type: 'select', workshopId: String(workshopId) });
    try {
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
            window.chrome.webview.postMessage(msg);
        }
    } catch (e) {}
}

window.driveCareFocusWorkshop = function (workshopId) {
    var pm = placemarks[workshopId];
    if (!pm || !window.driveCareMap) return;
    window.driveCareMap.setCenter(pm.geometry.getCoordinates(), 15, { duration: 300 });
    pm.balloon.open();
};

function selectLink(id) {
    return '<br/><a href=""#"" onclick=""notifyHost(\'' + id + '\'); return false;"">Выбрать</a>';
}

ymaps.ready(function () {
    var map = new ymaps.Map('map', {
        center: [" + lat + ", " + lon + @"],
        zoom: " + YandexMapsConfig.DefaultZoom + @",
        controls: ['zoomControl', 'searchControl', 'typeSelector', 'fullscreenControl'],
        behaviors: ['drag', 'scrollZoom', 'dblClickZoom', 'multiTouch']
    }, { suppressMapOpenBlock: true });

    window.driveCareMap = map;

    if (!pins || pins.length === 0) return;

    var bounds = [];
    for (var i = 0; i < pins.length; i++) {
        (function (p) {
            var coords = [p.Latitude, p.Longitude];
            bounds.push(coords);
            var body = (p.ServiceKindName ? ('<b>' + p.ServiceKindName + '</b><br/>') : '') +
                (p.AddressLine || '') +
                (p.Phone ? ('<br/>Тел.: ' + p.Phone) : '') +
                selectLink(p.WorkshopId);
            var pm = new ymaps.Placemark(coords, {
                workshopId: p.WorkshopId,
                balloonContentHeader: p.WorkshopName || 'Автосервис',
                balloonContentBody: body,
                hintContent: (p.ServiceKindName ? p.ServiceKindName + ': ' : '') + (p.WorkshopName || '')
            }, {
                preset: p.IconPreset || 'islands#blueAutoIcon',
                openBalloonOnClick: true
            });
            pm.events.add('click', function () { notifyHost(p.WorkshopId); });
            placemarks[p.WorkshopId] = pm;
            map.geoObjects.add(pm);
        })(pins[i]);
    }

    if (bounds.length === 1) {
        map.setCenter(bounds[0], 14);
    } else if (bounds.length > 1) {
        map.setBounds(ymaps.util.bounds.fromPoints(bounds), { checkZoomRange: true, zoomMargin: 50 });
    }
});
");
            sb.AppendLine("</script></body></html>");
            return sb.ToString();
        }
    }
}
