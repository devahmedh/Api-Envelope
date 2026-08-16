namespace TheSpectre.ApiEnvelope.AspNetCore;

/// <summary>
/// Marks a controller, action or endpoint whose responses must not be enveloped.
/// </summary>
/// <remarks>
/// Intended for health checks, file downloads, third-party callbacks whose response shape is
/// dictated by the caller, and any endpoint whose contract is fixed by something other than
/// this library.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class NoEnvelopeAttribute : Attribute;
