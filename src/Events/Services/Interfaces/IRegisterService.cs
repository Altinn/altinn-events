using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Services.Interfaces;

/// <summary>
/// Represents a type that can be used to handle communication with the Register application API.
/// </summary>
public interface IRegisterService
{
    /// <summary>
    /// Party lookup
    /// </summary>
    /// <param name="orgNo">organisation number</param>
    /// <param name="person">f or d number</param>
    /// <returns></returns>
    Task<int> PartyLookup(string orgNo, string person);

    /// <summary>
    /// Perform a register lookup with urn based party identifiers in order to find all other party identifiers.
    /// </summary>
    /// <param name="partyUrnList">List of urn values with a party identifying value</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used by other objects or threads to receive notice of cancellation.
    /// </param>
    /// <returns>A list of the perties found in register</returns>
    Task<IEnumerable<PartyIdentifiers>> PartyLookup(
        IEnumerable<string> partyUrnList, 
        CancellationToken cancellationToken);
}
