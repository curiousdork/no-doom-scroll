
using KeyValue.CLI.Commands;
using KeyValue.CLI.Service;
using KeyValue.CLI.Types;
using KeyValue.CLI.Types.Extensions;
using NSubstitute;
using Shouldly;

namespace KeyValue.CLI.Tests.Commands;

public class GetCommandsTests
{
    private readonly IKeyValueStore _kvs;
    private readonly GetCommands _sut;
    
    public GetCommandsTests()
    {
        _kvs = Substitute.For<IKeyValueStore>();
        _sut = new GetCommands(_kvs);
    }
    
    [Fact]
    public void ListOne_ReturnsEmptyWhenKeyNotFound() 
    {
        // Arrange
        var key = "TestKey";
        _kvs.GetOne(key).Returns(new None());
        
        // Act
        var result = _sut.ListOne(key);
        
        // Assert
        _kvs.Received().GetOne(Arg.Is<string>(key));
        result
        .IsNone()
        .ShouldBeTrue();
    }
    
    [Fact]
    public void ListAll_ReturnsEmptyCollectionWhenNoKeysExists() 
    {
        // Arrange
        _kvs.GetAll().Returns([]);
        
        // Act
        var result = _sut.ListAll();
        
        // Assert
        _kvs.Received().GetAll();
        result
        .ShouldBeEmpty();
    }
}