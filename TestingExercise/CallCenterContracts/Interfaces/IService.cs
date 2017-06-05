using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallCenterContracts
{
    public interface IService
    {
        CallData CallPrediction(string phoneNumber);
        Task<CallData> CallPredictionAsync(string phoneNumber);
        double RecomendedDiscount(CallData phoneNumber);
        Task<double> RecomendedDiscountAsync(CallData phoneNumber);
    }
}
