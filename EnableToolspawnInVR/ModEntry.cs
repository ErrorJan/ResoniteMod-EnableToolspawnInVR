using System.Collections.Generic;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System.Reflection.Emit;
using System;
using System.Reflection;

namespace EnableToolspawnInVR;
public class EnableToolspawnInVR : ResoniteMod {
    internal const string VERSION_CONSTANT = "1.0.1"; //Changing the version here updates it in all locations needed
    public override string Name => "Enable Toolspawn in VR";
    public override string Author => "ErrorJan";
    public override string Version => VERSION_CONSTANT;
    public override string Link => "https://github.com/ErrorJan/ResoniteMod-EnableToolspawnInVR/";

    public override void OnEngineInit() 
    {
        Harmony harmony = new Harmony("ErrorJan.EnableToolspawnInVR");
        harmony.PatchAll();
    }

    [HarmonyPatch( typeof( InteractionHandler ), "OnInputUpdate" )]
    class Patch
    {
        static IEnumerable<CodeInstruction> Transpiler( IEnumerable<CodeInstruction> a )
        {
            // Could be improved by remembering all instructions, which have a ret opcode and then later checking 
            //  for any of those branches jumping to one of the ret instructions or instead of a branch a ret would be there.
            // Another improvement would be checking if a return happens before the actual toolspawn code.

            List<CodeInstruction> opCodes = new ( a );
            // Remember the last opCode, which has to a be ret (return) OpCode
            var retInst = opCodes[ opCodes.Count - 1 ];
            if ( retInst.opcode != OpCodes.Ret )
            {
                Error( "Last instruction of InteractionHandler::OnInputUpdate isn't a ret??? Failed inject.." );
                foreach ( var i in opCodes )
                    yield return i;
            }
            else
            {
                // Helper function, which takes in a Label and checks if that label 
                // is the same as one of the labels at the ret instruction.
                Func<Label?, bool> isRetLabel = 
                    ( Label? checkLabel ) => 
                    { 
                        foreach ( var l in retInst.labels ) 
                            if ( l == checkLabel ) 
                                return true; 
                        return false; 
                    };

                MethodInfo screenActiveMethod = AccessTools.Method( typeof( InputInterface ), "get_ScreenActive" );
                int count = 0;

                // If we're calling get_ScreenActive, and the next instruction after that is a branch instruction,
                // which branches to a return instruction, then insert a Pop instruction to remove the boolean that was placed
                // onto the stack for the boolean check and remove the branch instruction.
                foreach ( var opCode in opCodes )
                {
                    if ( opCode.Calls( screenActiveMethod ) )
                        count = 1;

                    if ( count >= 0 && CodeInstructionExtensions.Branches( opCode, out Label? l ) )
                    {
                        if ( isRetLabel( l ) )
                        {
                            yield return new CodeInstruction( OpCodes.Pop );
                            // yield return new CodeInstruction( OpCodes.Brfalse, l ); // this is the instruction which would be there
                            // yield return new CodeInstruction( OpCodes.Brtrue, l );
                            count--;
                            continue;
                        }
                    }

                    count--;
                    yield return opCode;
                }
            }
        }
    }
}
