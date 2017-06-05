using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallCenterContracts
{
    public interface ICallBot
    {
        /// <summary>
        /// Starts the session.
        /// </summary>
        /// <param name="phoneNumber">The phone number.</param>
        /// <param name="channel">Interaction channel.</param>
        /// <returns>
        /// </returns>
        void StartSession(
                        string phoneNumber, 
                        Func<string, string> channel);

        /// <summary>
        /// Starts the session.
        /// </summary>
        /// <param name="phoneNumber">The phone number.</param>
        /// <param name="channel">Interaction channel.</param>
        /// <returns>
        /// </returns>
        Task StartSessionAsync(
                        string phoneNumber, 
                        Func<string, Task<string>> channel);

    }
}
