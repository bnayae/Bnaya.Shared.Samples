using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace TestingExercise
{
    [TestClass]
    public class CallBotTest
    {
        // TODO: define Mocks

        [TestInitialize]
        public void Setup()
        {
            // TODO: Initial mock (setup) with default behavior

        }

        [TestInitialize]
        public void Cleanup()
        {
        }

        [TestMethod]
        public void NormalConversation_Test()
        {
            // arrange
            // TODO: Override mock behavior (if needed)

            // act

            // verify
            // TODO: verify that all logs contains the expected content
            // TODO: verify that all service call got the expected input
            // TODO: verify that all repository call got the expected input
            throw new NotImplementedException();
        }

        [TestMethod]
        public async Task NormalAsyncConversation_Test()
        {
            // arrange
            // TODO: Override mock behavior (if needed)

            // act

            // verify
            // TODO: verify that all logs contains the expected content
            // TODO: verify that all service call got the expected input
            // TODO: verify that all repository call got the expected input
            throw new NotImplementedException();
        }

        [TestMethod]
        public async Task NormalAsyncConversation_WithDelayedResponse_Test()
        {
            // arrange
            // TODO: Override mock behavior 
            //       with delayed response (IStopwatch mock)

            // act

            // verify
            // TODO: verify that all logs contains the expected content
            // TODO: verify that all service call got the expected input
            // TODO: verify that all repository call got the expected input
            throw new NotImplementedException();
        }

        // TODO: add test for VIP and Waste of Time calls
        // TODO: add test for none deal
        // TODO: add test for deal when fee = 105 and fee = 96
        // TODO: add test for error case (channel Func throw)

        // TODO: Advance, detect dead lock when the channel Func or any mocked dependencies
        //              blocked and don't return response
    }
}
