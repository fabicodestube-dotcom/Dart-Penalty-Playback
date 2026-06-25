using System.Linq;
using TMPro;
using UnityEngine;

public class CheckoutHandler : MonoBehaviour
{
    public TMP_Text checkoutText;


    public void Show(string option)
    {
        if (option == null)
        {
            checkoutText.text = "-";
            return;
        }
        checkoutText.text = option;
    }
}
