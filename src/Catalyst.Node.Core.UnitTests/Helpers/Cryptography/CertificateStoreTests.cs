using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Catalyst.Node.Core.Helpers;
using Catalyst.Node.Core.Helpers.Cryptography;
using Xunit;
using Xunit.Abstractions;
using NSubstitute;
using FluentAssertions;
using Serilog;

namespace Catalyst.Node.Core.UnitTest.Helpers.Cryptography
{
    public class CertificateStoreTests : IDisposable
    {
        private string _fileWithPassName;
        private DirectoryInfo _directoryInfo;
        private CertificateStore _certificateStore;
        private X509Certificate2 _createdCertificate;
        private X509Certificate2 _retrievedCertificate;

        private readonly IPasswordReader _passwordReader;

        /// <summary>
        /// This can for instance be used to find the name of the test currently using the
        /// class by calling this.CurrentTest.DisplayName
        /// </summary>
        private readonly ITest _currentTest;
        private readonly string _currentTestName;
        private readonly ITestOutputHelper _output;
        private readonly IFileSystem _fileSystem;
        private SecureString _password;

        public CertificateStoreTests(ITestOutputHelper output)
        {
            _output = output;
            _currentTest = (_output?.GetType()
                                   .GetField("test", BindingFlags.Instance | BindingFlags.NonPublic)
                                   .GetValue(_output) as ITest);
            _currentTestName = _currentTest.TestCase.TestMethod.Method.Name;
            var testDirectory = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, _currentTestName));

            _fileSystem = Substitute.For<IFileSystem>();
            _fileSystem.GetCatalystHomeDir().Returns(testDirectory);

            _passwordReader = Substitute.For<IPasswordReader>();
        }


        private SecureString BuildSecureStringPassword()
        {
            var secureString = new SecureString();
            "password".ToList().ForEach(c => secureString.AppendChar(c));
            secureString.MakeReadOnly();
            return secureString;
        }

        [Fact]
        public void CertificateStore_CanReadAndWriteCertFiles_WithPassword()
        {
            //TODO: cf. issue <see cref="https://github.com/catalyst-network/Catalyst.Node/issues/2" />
            if (Environment.OSVersion.Platform == PlatformID.Unix) return;

            Create_certificate_store();
            Ensure_no_certificate_file_exists();
            Create_a_certificate_file_with_password();
            Read_the_certificate_file_with_password();
            The_store_should_have_asked_for_a_password_on_creation_and_loading();
            The_certificate_from_file_should_have_the_correct_thumbprint();
        }

        private void Create_certificate_store()
        {
            var dataFolder = Path.Combine(Environment.CurrentDirectory, _currentTestName);
            _directoryInfo = new DirectoryInfo(dataFolder);
            _certificateStore = new CertificateStore(_fileSystem, _passwordReader);
        }

        private void Ensure_no_certificate_file_exists()
        {
            var dataFolder = Path.Combine(Environment.CurrentDirectory, _currentTestName);
            _directoryInfo = new DirectoryInfo(dataFolder);
            if(_directoryInfo.Exists) _directoryInfo.Delete(true);
            _directoryInfo.Create();
            _directoryInfo.EnumerateFiles().Should().BeEmpty();

            _fileWithPassName = "test-with-pass.pfx";
            _certificateStore.TryGet(_fileWithPassName, out var _).Should().BeFalse();
        }

        private void Create_a_certificate_file_with_password()
        {
            _passwordReader.ReadSecurePassword().Returns(BuildSecureStringPassword());
            _createdCertificate = _certificateStore.CreateAndSaveSelfSignedCertificate(_fileWithPassName);
        }
        
        private void The_store_should_have_asked_for_a_password_on_creation_and_loading()
        {
            _passwordReader.ReceivedWithAnyArgs(2).ReadSecurePassword(null);
        }

        private void Read_the_certificate_file_with_password()
        {
            _passwordReader.ReadSecurePassword().Returns(BuildSecureStringPassword());
            _retrievedCertificate = null;
            _certificateStore.TryGet(_fileWithPassName, out _retrievedCertificate).Should().BeTrue();
        }

        private void The_certificate_from_file_should_have_the_correct_thumbprint()
        {
            _retrievedCertificate.Should().NotBeNull();
            _retrievedCertificate.Thumbprint.Should().Be(_createdCertificate.Thumbprint);
        }

        public void Dispose()
        {
            _createdCertificate?.Dispose();
            _retrievedCertificate?.Dispose();
            _password?.Dispose();
        }
    }
}