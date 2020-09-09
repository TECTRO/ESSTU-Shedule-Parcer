using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace ParseHelper
{
    public partial class ScheduleParser
    {
        public readonly AsyncMethods Async;

        public class AsyncMethods
        {
            private readonly ScheduleParser _parent;
            internal AsyncMethods(ScheduleParser parent) => _parent = parent;
            //===============================================================================================
          
            public void LoadSchedulesRecurcieveAsync(string website, NodeType type, ICollection<Schedule> result)
            {
                GetLinksRecursiveAsync(website, null, (l, d) =>
                {
                    foreach (var schedule in _parent.FillTable(type, d))
                        result.Add(schedule);
                });
            }
            public void LoadSchedulesRecurcieveAsync(IEnumerable<string> websites, NodeType type, ICollection<Schedule> result)
            {
                foreach (var website in websites)
                {
                    _parent.ThManager.Add(new Thread(() => LoadSchedulesRecurcieveAsync(website, type, result)));
                }
            }
            private void LoadSchedulesAsync(string tableLink, NodeType type, ICollection<Schedule> result)
            {
                Thread local = new Thread(() =>
                {
                    using (WebClient client = new WebClient())
                    {
                        try
                        {
                            var preResult = _parent.FillTable(type, client.DownloadString(tableLink));

                            lock (_parent._synchronizationPlug)
                            {
                                foreach (var schedule in preResult)
                                    result.Add(schedule);
                            }
                        }
                        catch (Exception e)
                        {
                            _parent.ExceptionEvent?.Invoke(e);
                        }
                    }
                });
                _parent.ThManager.Add(local);
            }
            private void GetLinksRecursiveAsync(string website, ICollection<string> result, GerLinksDelegate inputFunctionDelegate = null)
            {
                string mask = WebLinkMask.Match(website).Groups["mask"].Value + '/';

                using (WebClient client = new WebClient())
                {
                    string mainData;

                    try
                    {
                        mainData = client.DownloadString(website);
                    }
                    catch (Exception e)
                    {
                        _parent.ExceptionEvent?.Invoke(e);
                        return;
                    }

                    var filteredMatches = WebLinkAddress.Matches(mainData).Cast<Match>()
                        .Where(t => !t.Groups["address"].Value.Contains("http") && t.Groups["linkname"].Value
                                        .Any(c => char.IsDigit(c) || char.IsLetter(c)))
                        .Select(t => t.Groups["address"].Value)
                        .Distinct()
                        .ToList();

                    if (!filteredMatches.Any())
                    {
                        filteredMatches = WebLinkAddressShort.Matches(mainData).Cast<Match>()
                            .Where(t => !t.Groups["address"].Value.Contains("http"))
                            .Select(t => t.Groups["address"].Value)
                            .Distinct()
                            .ToList();
                    }

                    if (filteredMatches.Any())
                    {
                        foreach (var match in filteredMatches)
                        {
                            _parent.ThManager.Add(
                                new Thread(() =>
                                {
                                    GetLinksRecursiveAsync(mask + match, result, inputFunctionDelegate);

                                }));
                        }
                    }
                    else
                    {
                        lock (_parent._synchronizationPlug)
                        {
                            result?.Add(website);
                        }

                        inputFunctionDelegate?.Invoke( website , mainData);
                    }
                }
            }
        }
    }
}