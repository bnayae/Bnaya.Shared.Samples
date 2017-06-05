using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CallCenterContracts;

namespace CallCenterLogic
{
    public class CallBot : ICallBot
    {
        // TODO: inject the following dependencies
        private readonly IStopwatch _stopwatch;
        private readonly IService _service;
        private readonly ILogger _logger;
        private readonly IRepository _repository;

        public readonly string[] CONVERSATION_PATH =
            {
                "The price is 105",
                "We can offer 10 minutes for free",
                "Would you accept the offer",
                "Our last offer is 96",
                "It's the best offer I can give",
                "We will call you in 1 hour",
            };

        public void StartSession(
            string phoneNumber,
            Func<string, string> channel)
        {
            try
            {
                var sessionId = Guid.NewGuid();
                _stopwatch.Start();
                Deal currentDeal = _repository.Load(phoneNumber);
                CallData prediction =
                    _service.CallPrediction(phoneNumber);

                _logger.Log(LogSeverity.Info, $"start session [{phoneNumber}]");

                string response = null;
                switch (prediction.Type)
                {
                    case CustomerType.VIP:
                        response = channel("How Can I help you");
                        break;
                    case CustomerType.Normal:
                        response = channel("Hello");
                        break;
                    case CustomerType.WasteOfTime:
                        response = channel("Your place is 30, please leave a message");
                        return;
                }

                int i = 0;
                while (i < CONVERSATION_PATH.Length &&
                                response != null &&
                                response != "deal")
                {
                    TimeSpan duration = _stopwatch.Duration;
                    string answer = CONVERSATION_PATH[i];
                    response = channel(answer);
                    _logger.Log(LogSeverity.Info, $"Conversation [{phoneNumber}]: {answer} -> {response}");
                    if (_stopwatch.Duration - duration > TimeSpan.FromSeconds(20))
                        _logger.Log(LogSeverity.Warn, "Long delay");
                    i++;
                }

                if (response == "deal")
                {
                    int id = currentDeal?.Id ?? -1;
                    double fee = 105;
                    if (i > 2)
                        fee = 96;
                    var newDeal = new Deal(id, sessionId, phoneNumber, fee);
                    _repository.Save(newDeal);
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogSeverity.Error, ex);
            }
        }

        public async Task StartSessionAsync(
            string phoneNumber,
            Func<string, Task<string>> channel)
        {
            try
            {
                var sessionId = Guid.NewGuid();
                _stopwatch.Start();
                Deal currentDeal = await _repository.LoadAsync(phoneNumber);
                CallData prediction =
                    await _service.CallPredictionAsync(phoneNumber);

                _logger.Log(LogSeverity.Info, $"start session [{phoneNumber}]");

                string response = null;
                switch (prediction.Type)
                {
                    case CustomerType.VIP:
                        response = await channel("How Can I help you");
                        break;
                    case CustomerType.Normal:
                        response = await channel("Hello");
                        break;
                    case CustomerType.WasteOfTime:
                        response = await channel("Your place is 30, please leave a message");
                        return;
                }

                int i = 0;
                while (i < CONVERSATION_PATH.Length &&
                                response != null &&
                                response != "deal")
                {
                    TimeSpan duration = _stopwatch.Duration;
                    string answer = CONVERSATION_PATH[i];
                    response = await channel(answer);
                    _logger.Log(LogSeverity.Info, $"Conversation [{phoneNumber}]: {answer} -> {response}");
                    if (_stopwatch.Duration - duration > TimeSpan.FromSeconds(20))
                        _logger.Log(LogSeverity.Warn, "Long delay");
                    i++;
                }

                if (response == "deal")
                {
                    int id = currentDeal?.Id ?? -1;
                    double fee = 105;
                    if (i > 2)
                        fee = 96;
                    var newDeal = new Deal(id, sessionId, phoneNumber, fee);
                    await _repository.SaveAsync(newDeal);
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogSeverity.Error, ex);
            }
        }
    }
}
