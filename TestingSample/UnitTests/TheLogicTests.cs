using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using MyLogic;

namespace UnitTests
{
    [TestClass]
    public class TheLogicTests
    {
        private Mock<IRepository> _repositoryMock = new Mock<IRepository>();
        private Mock<ILogger> _loggerMock = new Mock<ILogger>();

        [TestInitialize]
        public void Setup()
        {
            //_repositoryMock.Setup(m => m.LoadFactor("A"))
            _repositoryMock.Setup(m => m.LoadFactor(It.IsAny<string>()))
                           .Returns(() => 2);
            _repositoryMock.Setup(m => m.LoadFactorAsync(It.IsAny<string>()))
                           .ReturnsAsync(() => 2);
        }

        [TestMethod]
        public void Logic_With30_Test()
        {
            // arrange
            var logic = new TheLogic(
                _repositoryMock.Object,
                _loggerMock.Object);

            // act
            double result = logic.Calc(30);

            // assert
            Assert.AreEqual(60, result, "fail to check 30");
            _loggerMock.Verify(m => m.Log(SeverityLevel.Info,
                                            It.IsAny<string>()), Times.Once(), "Log");
            _loggerMock.Verify(m => m.Log(SeverityLevel.Error,
                                            It.IsAny<string>()), Times.Never(), "Log");
        }

        [TestMethod]
        public async Task LogicAsync_With30_Test()
        {
            // arrange
            var logic = new TheLogic(
                _repositoryMock.Object,
                _loggerMock.Object);

            // act
            double result = await logic.CalcAsync(30);

            // assert
            Assert.AreEqual(60, result, "fail to check 30");
            _loggerMock.Verify(m => m.Log(SeverityLevel.Info,
                                            It.IsAny<string>()), Times.Once(), "Log");
            _loggerMock.Verify(m => m.Log(SeverityLevel.Error,
                                            It.IsAny<string>()), Times.Never(), "Log");
        }

        [TestMethod]
        //[ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Logic_Throw_Test()
        {
            // arrange
            var logic = new TheLogic(
                _repositoryMock.Object,
                _loggerMock.Object);
            _loggerMock.Setup(m => m.Log(SeverityLevel.Info, It.IsAny<string>()))
                        .Throws<ArgumentOutOfRangeException>();

            try
            {
                double result = logic.Calc(30);
                throw new Exception();
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // exception
            }
            // act

            // assert
            _loggerMock.Verify(m => m.Log(SeverityLevel.Info,
                                            It.IsAny<string>()), Times.Once(), "Log");
            _loggerMock.Verify(m => m.Log(SeverityLevel.Error,
                                            It.IsAny<string>()), Times.Once(), "Log");
        }
    }
}
