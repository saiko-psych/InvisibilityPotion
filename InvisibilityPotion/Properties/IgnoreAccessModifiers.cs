// Lets the Mono JIT skip access checks so members made public by the publicizer
// at compile time can be used against the original game assembly at runtime.
// Pattern from JotunnModStub (MIT-0).
using System.Security.Permissions;

#pragma warning disable CS0618 // SecurityPermission is obsolete but still honoured by Mono
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
