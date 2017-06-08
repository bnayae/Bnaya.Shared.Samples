using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyLogic
{
    public class TheLogic
    {
        private const string TYPE = "A";
        private readonly IRepository _repository;
        private readonly ILogger _logger;

        public TheLogic(
            IRepository repository,
            ILogger logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public double Calc(double a)
        {
            try
            {
                double factor = _repository.LoadFactor(TYPE);
                double result = a * factor;
                _logger.Log(SeverityLevel.Info, $"Ok of {a}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Log(SeverityLevel.Error, $"Fail: {ex}");
                throw;
            }
        }

        public async Task<double> CalcAsync(double a)
        {
            try
            {
                double factor = await _repository.LoadFactorAsync(TYPE);
                double result = a * factor;
                _logger.Log(SeverityLevel.Info, $"Ok of {a}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Log(SeverityLevel.Error, $"Fail: {ex}");
                throw;
            }
        }

    }
}
