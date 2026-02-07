using Xunit;
using Moq;
using RiskInsure.NsbShipping.Domain.Managers;
using RiskInsure.NsbShipping.Domain.Models;
using RiskInsure.NsbShipping.Domain.Repositories;
using Microsoft.Extensions.Logging;

public class ShippingManagerTests
{
    [Fact]
    public async Task ShipOrderAsync_DuplicateOrder_DoesNotCreateDuplicate()
    {
        var repo = new Mock<IShipmentRepository>();
        var logger = new Mock<ILogger<ShippingManager>>();
        var existing = new Shipment { Id = "id", OrderId = Guid.NewGuid() };
        repo.Setup(r => r.GetByOrderIdAsync(existing.OrderId)).ReturnsAsync(existing);
        var manager = new ShippingManager(repo.Object, logger.Object);
        var result = await manager.ShipOrderAsync(existing.OrderId);
        repo.Verify(r => r.CreateAsync(It.IsAny<Shipment>()), Times.Never);
        Assert.Equal(existing.OrderId, result.OrderId);
    }
}
