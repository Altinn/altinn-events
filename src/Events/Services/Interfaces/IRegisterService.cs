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

    /// <summary>
    /// Retrieves party information of the main unit associated with the specified organization number.
    /// </summary>
    /// <remarks>
    /// If multiple main units are returned for the specified organization number, only the
    /// first result is used. The method performs an HTTP POST request to the underlying service.
    /// </remarks>
    /// <param name="orgNumber">The organization number for which to look up the main unit. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the organization number of the
    /// main unit if found; otherwise, null.
    /// </returns>
    Task<OrganizationRecord> GetMainUnit(string orgNumber, CancellationToken cancellationToken);
}
