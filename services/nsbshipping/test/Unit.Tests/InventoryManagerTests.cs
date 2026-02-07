using Xunit;
using Moq;
using RiskInsure.NsbShipping.Domain.Managers;
using RiskInsure.NsbShipping.Domain.Models;
using RiskInsure.NsbShipping.Domain.Repositories;
using Microsoft.Extensions.Logging;

public class InventoryManagerTests
{
    [Fact]
    public async Task ReserveInventoryAsync_DuplicateOrder_DoesNotCreateDuplicate()
    {
        var repo = new Mock<IInventoryReservationRepository>();
        var logger = new Mock<ILogger<InventoryManager>>();
        var existing = new InventoryReservation { Id = "id", OrderId = Guid.NewGuid() };
        repo.Setup(r => r.GetByOrderIdAsync(existing.OrderId)).ReturnsAsync(existing);
        var manager = new InventoryManager(repo.Object, logger.Object);
        var result = await manager.ReserveInventoryAsync(existing.OrderId);
        repo.Verify(r => r.CreateAsync(It.IsAny<InventoryReservation>()), Times.Never);
        Assert.Equal(existing.OrderId, result.OrderId);
    }
}
