using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallCenterContracts
{
    public class Deal
    {
        public Deal(int id, Guid sessionId, string phoneNumber, double fee)
        {
            Id = id;
            SessionId = sessionId;
            PhoneNumber = phoneNumber;
            Fee = fee;
        }
        public int Id { get; }
        public Guid SessionId { get; }
        public string PhoneNumber { get; }
        public double Fee { get; }
    }
}
