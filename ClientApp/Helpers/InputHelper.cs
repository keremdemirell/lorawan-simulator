using Serilog;

public static class InputHelper
{

    public static int GetIntInput(string prompt)
    {
        return GetIntInput(prompt, 0);
    }

    public static int GetIntInput(string prompt, int maxChoice)
    {
        while (true)
        {
            Console.WriteLine(prompt);

            try
            {
                int input = int.Parse(Console.ReadLine().Trim());

                if (maxChoice != 0)
                {
                    if (input >= 1 && input <= maxChoice)
                    {
                        return input;
                    }
                    else
                    {
                        Log.Warning($"Invalid input. Please enter a number between 1 and {maxChoice}.");
                    }
                }
                else
                {
                    return input;
                }
            }
            catch (FormatException ex)
            {
                Log.Error(ex, "Invalid input. Please enter a valid number.");
            }
        }
    }

    public static string ValidateDeviceInformationInput(string prompt, DeviceInfoType deviceInfo)
    {
        while (true)
        {
            Console.WriteLine(prompt);
            string input = Console.ReadLine().Trim();

            // int inputLength = deviceInfo.Trim().ToUpper() == "DEVADDR" ? 10 : 34;
            int inputLength = deviceInfo == DeviceInfoType.DevAddr ? 10 : 34;

            if (input.StartsWith("0x") && input.Length == inputLength && IsHex(input.Substring(2)))
            {
                return input;
            }
            else
            {
                Log.Warning($"Invalid {deviceInfo.ToString()}. Please enter a value with {inputLength} hex characters including '0x'.");
            }

        }
    }

    public static bool IsHex(string input)
    {
        foreach (char c in input)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
            {
                return false;
            }
        }
        return true;
    }

}