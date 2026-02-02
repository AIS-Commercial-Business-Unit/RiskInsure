using System;
using System.Linq;
using Xunit;
using Beneficiary.Domain.DTOs;

namespace Test
{
    public class BeneficiaryRegistrationDtoTests
    {
        [Fact]
        public void FirstName_WithValidLength_PassesValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.FirstName = "John"; // 4 characters - within 40 char limit

            // Act
            var results = dto.Validate();

            // Assert
            var firstNameErrors = results.Where(r => r.MemberNames.Contains("FirstName")).ToList();
            Assert.Empty(firstNameErrors);
        }

        [Fact]
        public void FirstName_WithMaxLength_PassesValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.FirstName = "1234567890123456789012345678901234567890123"; // Exactly 43 characters

            // Act
            var results = dto.Validate();

            // Assert
            var firstNameErrors = results.Where(r => r.MemberNames.Contains("FirstName")).ToList();
            Assert.Empty(firstNameErrors);
        }

        [Fact]
        public void FirstName_ExceedsMaxLength_FailsValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.FirstName = "12345678901234567890123456789012345678901234"; // 44 characters - exceeds 43 char limit

            // Act
            var results = dto.Validate();

            // Assert
            var firstNameErrors = results.Where(r => r.MemberNames.Contains("FirstName")).ToList();
            Assert.NotEmpty(firstNameErrors);
            Assert.Contains("First name cannot exceed 43 characters", firstNameErrors[0].ErrorMessage);
        }

        [Fact]
        public void FirstName_Empty_FailsValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.FirstName = string.Empty;

            // Act
            var results = dto.Validate();

            // Assert
            var firstNameErrors = results.Where(r => r.MemberNames.Contains("FirstName")).ToList();
            Assert.NotEmpty(firstNameErrors);
            Assert.Contains("First name is required", firstNameErrors[0].ErrorMessage);
        }

        [Fact]
        public void LastName_WithValidLength_PassesValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.LastName = "Smith"; // Within 100 char limit

            // Act
            var results = dto.Validate();

            // Assert
            var lastNameErrors = results.Where(r => r.MemberNames.Contains("LastName")).ToList();
            Assert.Empty(lastNameErrors);
        }

        [Fact]
        public void DateOfBirth_ValidFormat_PassesValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.DateOfBirth = "1990-01-15";

            // Act
            var results = dto.Validate();

            // Assert
            var dobErrors = results.Where(r => r.MemberNames.Contains("DateOfBirth")).ToList();
            Assert.Empty(dobErrors);
        }

        [Fact]
        public void DateOfBirth_InvalidFormat_FailsValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.DateOfBirth = "15/01/1990"; // Wrong format

            // Act
            var results = dto.Validate();

            // Assert
            var dobErrors = results.Where(r => r.MemberNames.Contains("DateOfBirth")).ToList();
            Assert.NotEmpty(dobErrors);
            Assert.Contains("Date of birth must be in YYYY-MM-DD format", dobErrors[0].ErrorMessage);
        }

        [Fact]
        public void DateOfBirth_FutureDate_FailsValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.DateOfBirth = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

            // Act
            var results = dto.Validate();

            // Assert
            var dobErrors = results.Where(r => r.MemberNames.Contains("DateOfBirth")).ToList();
            Assert.NotEmpty(dobErrors);
            Assert.Contains("Date of birth cannot be in the future", dobErrors[0].ErrorMessage);
        }

        [Fact]
        public void Email_ValidFormat_PassesValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Email = "test@example.com";

            // Act
            var results = dto.Validate();

            // Assert
            var emailErrors = results.Where(r => r.MemberNames.Contains("Email")).ToList();
            Assert.Empty(emailErrors);
        }

        [Fact]
        public void Email_InvalidFormat_FailsValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Email = "invalid-email";

            // Act
            var results = dto.Validate();

            // Assert
            var emailErrors = results.Where(r => r.MemberNames.Contains("Email")).ToList();
            Assert.NotEmpty(emailErrors);
        }

        [Fact]
        public void CaseStatus_ValidValue_PassesValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.CaseStatus = "ACTIVE";

            // Act
            var results = dto.Validate();

            // Assert
            var statusErrors = results.Where(r => r.MemberNames.Contains("CaseStatus")).ToList();
            Assert.Empty(statusErrors);
        }

        [Fact]
        public void CaseStatus_InvalidValue_FailsValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.CaseStatus = "INVALID_STATUS";

            // Act
            var results = dto.Validate();

            // Assert
            var statusErrors = results.Where(r => r.MemberNames.Contains("CaseStatus")).ToList();
            Assert.NotEmpty(statusErrors);
        }

        [Fact]
        public void DocumentNumber_ValidLength_PassesValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.DocumentNumber = "ABC123"; // Between 3 and 50 characters

            // Act
            var results = dto.Validate();

            // Assert
            var docErrors = results.Where(r => r.MemberNames.Contains("DocumentNumber")).ToList();
            Assert.Empty(docErrors);
        }

        [Fact]
        public void DocumentNumber_TooShort_FailsValidation()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.DocumentNumber = "AB"; // Less than 3 characters

            // Act
            var results = dto.Validate();

            // Assert
            var docErrors = results.Where(r => r.MemberNames.Contains("DocumentNumber")).ToList();
            Assert.NotEmpty(docErrors);
        }

        private BeneficiaryRegistrationDto CreateValidDto()
        {
            return new BeneficiaryRegistrationDto
            {
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = "1990-01-01",
                Nationality = "USA",
                DocumentType = "Passport",
                DocumentNumber = "ABC123456",
                CaseStatus = "PENDING"
            };
        }
    }
}
