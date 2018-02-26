using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.Extensions.Configuration;

using Data = Google.Apis.Sheets.v4.Data;

namespace DiscordBot_Jane.Core.Services
{
    public class GDocsService
    {

        private readonly DiscordSocketClient _discord;
        private readonly GoogleAuthenticateService _gAuth;
        private readonly CommandService _commands;
        private readonly LoggingService _logger;
        private readonly IServiceProvider _provider;
        private readonly ChannelService _channelService;
        private readonly IConfigurationRoot _config;
        private readonly Random _random;

        private const string ApplicationName = "LBS Jane Discord Bot";

        private string SheetName => $"{DateTime.Now:yyyy-MM-dd}";

        public readonly CancellationTokenSource CancellationToken = new CancellationTokenSource();

        public GDocsService(
            DiscordSocketClient discord,
            GoogleAuthenticateService gAuth,
            CommandService commands,
            LoggingService logger,
            IServiceProvider provider,
            ChannelService channelService,
            IConfigurationRoot config,
            Random random)
        {
            _discord = discord;
            _gAuth = gAuth;
            _commands = commands;
            _logger = logger;
            _provider = provider;
            _channelService = channelService;
            _config = config;
            _random = random;

            _gAuth.GoogleAuthenticated += StartUpdateLogSheetTask;
        }

        private async void StartUpdateLogSheetTask(object sender, UserCredential credential)
        {
            _gAuth.GoogleAuthenticated -= StartUpdateLogSheetTask;

            // Create Google Sheets API service.
            using (var service = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            }))
            {
                /* 
                 * This loop does not end until application exits.
                 * This is the order of operations:
                 * 1. Update log sheet.
                 * 2. Sleep for a period of time.
                 * 3. Repeat.
                 */
                while (!CancellationToken.IsCancellationRequested)
                {

                    // Time how long updating the log sheet takes.
                    var stopWatch = new Stopwatch();
                    await _logger.LogAsync(LogSeverity.Info, nameof(GDocsService),
                            $"Updating log sheet... Next update at {DateTime.Now.Add(TimeSpan.FromMinutes(10)):HH:mm}")
                        .ConfigureAwait(false);
                    stopWatch.Start();
                    // Update log sheet.
                    await UpdateLogSheet(service);
                    // Stop timer and log how long updating the log sheet took.
                    stopWatch.Stop();
                    await _logger.LogAsync(LogSeverity.Info, nameof(GDocsService),
                        $"Log sheet update completed! Took {stopWatch.ElapsedMilliseconds} ms").ConfigureAwait(false);

                    // Wait for a period of time before repeating.
                    try
                    {
                        var interval = _config.GetValue("sheetlogs_interval_minutes", 1.0);
                        await Task.Delay(TimeSpan.FromMinutes(interval), CancellationToken.Token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException e)
                    {
                        await _logger.LogAsync(LogSeverity.Info, nameof(GDocsService), e.Message).ConfigureAwait(false);
                        break;
                    }
                }
            }
        }

        private async Task UpdateLogSheet(SheetsService service)
        {
            // The ID of the spreadsheet to update.
            var spreadsheetId = _config["log_spreadsheet_id"];

            var data = _logger.GetLogData(_config.GetValue("log_spreadsheet_reverse", true));

            // Delete all rows except the top one.
            var height = GetHeight(service, spreadsheetId);
            var deleteResponse = await DeleteGoogleSheetRowsInBatch(spreadsheetId, height, _config, service);

            // Clear top row of data.
            var clearResponse = await ClearGoogleSheetInBatch(spreadsheetId, service);

            // Insert new rows of data.
            string range = GetRange(service, spreadsheetId);
            var updateResponse = await UpdatGoogleSheetInBatch(data, spreadsheetId, range, service);
        }

        private static async Task<Data.AppendValuesResponse> UpdatGoogleSheetInBatch(
            IList<IList<Object>> values,
            string spreadsheetId, 
            string newRange, 
            SheetsService service)
        {
            SpreadsheetsResource.ValuesResource.AppendRequest request =
                service.Spreadsheets.Values.Append(new Data.ValueRange { Values = values }, spreadsheetId, newRange);
            request.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.OVERWRITE;
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            return await request.ExecuteAsync();
        }

        private static async Task<Data.BatchClearValuesResponse> ClearGoogleSheetInBatch(
            string spreadsheetId,
            SheetsService service)
        {
            // The ranges to clear, in A1 notation.
            var ranges = new List<string>
            {
                "A2:C2"
            };
            
            var requestBody = new Data.BatchClearValuesRequest();
            requestBody.Ranges = ranges;

            var request = service.Spreadsheets.Values.BatchClear(requestBody, spreadsheetId);
            
            return await request.ExecuteAsync();
        }

        private static async Task<Data.BatchUpdateSpreadsheetResponse> DeleteGoogleSheetRowsInBatch(
            string spreadsheetId,
            int height,
            IConfigurationRoot config,
            SheetsService service)
        {
            if (height < 4) return null;

            var requestBody = new Data.Request()
            {
                DeleteDimension = new Data.DeleteDimensionRequest()
                {
                    Range = new Data.DimensionRange()
                    {
                        SheetId = 0,
                        Dimension = "ROWS",
                        StartIndex = config.GetValue("log_spreadsheet_delete_row_start_index", 0),
                        EndIndex = height
                    }
                }
            };

            var requestContainer = new List<Data.Request>();
            requestContainer.Add(requestBody);

            var deleteRequest = new Data.BatchUpdateSpreadsheetRequest();
            deleteRequest.Requests = requestContainer;
            
            var deletion = new SpreadsheetsResource.BatchUpdateRequest(service, deleteRequest, spreadsheetId);
            return await deletion.ExecuteAsync();
        }

        /// <summary>
        /// The A1 notation of a range to search for a logical table of data.
        /// Values will be appended after the last row of the table.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="spreadsheetId"></param>
        /// <returns></returns>
        private static string GetRange(SheetsService service, string spreadsheetId)
        {
            // Define request parameters.
            String range = "A:C";

            SpreadsheetsResource.ValuesResource.GetRequest getRequest =
                service.Spreadsheets.Values.Get(spreadsheetId, range);

            Data.ValueRange getResponse = getRequest.Execute();
            IList<IList<Object>> getValues = getResponse.Values;

            int currentCount = getValues.Count + 1;

            String newRange = "A" + currentCount + ":C";

            return newRange;
        }

        private static int GetHeight(SheetsService service, string spreadsheetId)
        {
            // Define request parameters.
            String range = "A:C";

            SpreadsheetsResource.ValuesResource.GetRequest getRequest =
                service.Spreadsheets.Values.Get(spreadsheetId, range);

            Data.ValueRange getResponse = getRequest.Execute();
            IList<IList<Object>> getValues = getResponse.Values;

            return getValues.Count + 1;
        }
    }
}
