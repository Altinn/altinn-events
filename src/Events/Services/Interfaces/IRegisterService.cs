using System.Collections.Generic;
using System.Threading.Tasks;

namespace Altinn.Platform.Events.Services.Interfaces;

/// <summary>
/// Interface to handle services exposed in Platform Register
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
    /// Perform a register lookup based on urn based party identifiers
    /// </summary>
    /// <param name="partyUrnList">List of urn strings with a party identifying value</param>
    /// <returns>A list of the perties found in register</returns>
    Task<IEnumerable<PartyIdentifiers>> PartyLookup(IEnumerable<string> partyUrnList);
}
