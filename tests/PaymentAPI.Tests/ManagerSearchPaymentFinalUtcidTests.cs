using Xunit;

namespace PaymentAPI.Tests;

public class ManagerSearchPaymentFinalUtcidTests
{
 [Theory]
 [InlineData("FUNC209-MGR-UTCID01",1)][InlineData("FUNC209-MGR-UTCID02",2)][InlineData("FUNC209-MGR-UTCID03",3)][InlineData("FUNC209-MGR-UTCID04",4)][InlineData("FUNC209-MGR-UTCID05",5)][InlineData("FUNC209-MGR-UTCID06",6)]
 public async Task Search_payment(string id,int n)
 {
  var t=new ManagerFilterPaymentUtcidTests();
  await(n switch{1=>t.Filter_user_and_keyword(),2=>t.Filter_status(),3=>t.Filter_payment_method(),4=>t.Combined_filters_return_empty_when_no_match(),5=>t.Filter_amount_boundaries_are_inclusive(),_=>t.Filter_date_range_is_inclusive_of_end_day()});
  Assert.StartsWith("FUNC209-MGR-UTCID",id);
 }
}
