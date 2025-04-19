using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TabNavigation : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        // 只在按下 Tab 时触发
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // 获取当前选中的 GameObject
            var current = EventSystem.current.currentSelectedGameObject;
            if (current == null) return;

            // 从它的 Selectable 找到下一个可导航的 UI 元件
            var sel = current.GetComponent<Selectable>();
            if (sel == null) return;

            // 这里用 FindSelectableOnDown()，你也可以根据布局改成 OnRight / OnUp / OnLeft
            var next = sel.FindSelectableOnRight();

            if (next != null)
            {
                // 让 EventSystem 选中下一个
                EventSystem.current.SetSelectedGameObject(next.gameObject);
                // 并且激活它的输入框
                var inputField = next.GetComponent<InputField>();
                if (inputField != null) inputField.ActivateInputField();

                var tmpInput = next.GetComponent<TMP_InputField>();
                if (tmpInput != null) tmpInput.ActivateInputField();
            }
        }
    }
}
