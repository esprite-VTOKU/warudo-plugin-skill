using UnityEngine;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;
using Warudo.Core.Plugins;

[PluginType(
    Id = "com.test.helloworld",
    Name = "Hello World",
    Description = "A code-only Warudo plugin built without Unity.",
    Author = "Test",
    Version = "1.0.0",
    NodeTypes = new System.Type[] { typeof(HelloWorldNode) }
)]
public class HelloWorldPlugin : Plugin
{
    protected override void OnCreate()
    {
        base.OnCreate();
        Debug.Log("[HelloWorld] Plugin loaded — no Unity required!");
    }

    protected override void OnDestroy()
    {
        Debug.Log("[HelloWorld] Plugin unloaded.");
        base.OnDestroy();
    }
}

[NodeType(
    Id = "com.test.helloworld.node",
    Title = "Hello World",
    Category = "Test",
    Width = 1f
)]
public class HelloWorldNode : Node
{
    [FlowInput]
    [Label("Enter")]
    public Continuation Enter()
    {
        Debug.Log("[HelloWorld] Node triggered!");
        return Exit;
    }

    [FlowOutput]
    [Label("Exit")]
    public Continuation Exit;

    [DataInput]
    [Label("Message")]
    public string Message = "Hello from no-Unity plugin!";

    [DataOutput]
    [Label("Message Out")]
    public string GetMessage() => Message;
}
