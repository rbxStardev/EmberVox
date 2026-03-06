# HARDCODE IS DEAD, ABSTRACTION IS FUEL, THE API IS FULL.
### `OBJECTIVE: ABSTRACT INTO FULL VULKAN API`

---

# Flame 🔥

> *You shouldn't have to fight your own renderer to draw a triangle.*

Flame is a Vulkan-based rendering API built for the EmberVox engine.
Not a renderer. Not an entity that does what it wants.
A tool. **Your** tool.

---

## The Problem

Most rendering code ends up like this: a monolithic class that initializes everything, owns everything, and decides everything. You don't use it — you watch it run.

That's not an API. That's a black box with a `MainLoop()`.

---

## The Philosophy

Flame takes one idea seriously:

**You describe. Flame executes.**

Nothing gets drawn without your permission. No pipeline is created behind your back. No loop runs without you starting it. Flame holds the Vulkan complexity so you never have to — but it never takes the wheel.

Think of it like Vulkan itself: explicit, honest, and completely under your control. Just... without the 800 lines of boilerplate.

---

## What It Looks Like

```csharp
var flame = new FlameDevice(window);

var mesh = flame.CreateBuffer(vertices);
var shader = flame.CreateShader("vert.spv", "frag.spv");
var pipeline = flame.CreatePipeline(new PipelineDesc
{
    Shader = shader,
    VertexLayout = VertexLayout.Of<Vertex>(),
});

window.Render += delta =>
{
    var cmd = flame.BeginFrame();
        cmd.SetPipeline(pipeline);
        cmd.Draw(mesh);
    flame.EndFrame();
};
```

That's it. You're not fighting Vulkan. You're just drawing things.

---

## What Flame Does

- Creates and manages Vulkan resources — buffers, shaders, pipelines, swapchains
- Exposes a clean command API for the frame loop
- Handles synchronization, memory allocation, and disposal internally
- Stays out of your way everywhere else

## What Flame Doesn't Do

- Doesn't own your loop
- Doesn't load your models
- Doesn't decide your shaders
- Doesn't know what a "tree" or a "character" is — and it shouldn't

---

## Status

Early. Honest. One week old.

The foundation is there — Vulkan initializes, the pipeline runs, a triangle is on screen.
Everything hardcoded today is a target for tomorrow.

The road is long. But the direction is clear.

---

*Built on [Silk.NET](https://github.com/dotnet/Silk.NET). Part of the EmberVox engine.*