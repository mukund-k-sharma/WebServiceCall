Imports System.CodeDom
Imports System.CodeDom.Compiler
Imports System.Reflection
Imports System.ServiceModel.Description
Imports System.Collections.ObjectModel
Imports System.ServiceModel
Imports System.Globalization
Imports System.IO
Imports System.Runtime.Serialization

Public Class ServiceDetails
    Public WSDLUri As Uri
    Public methodName As String
    Public serviceUri As Uri
    Public contractName As String

    Public Sub New(uri1 As Uri, uri2 As Uri, methodName As String, v As String)
        Me.WSDLUri = uri1
        Me.serviceUri = uri2
        Me.methodName = methodName
        Me.contractName = v
    End Sub
End Class



Friend Class DynamicWebService

    Public Function CallWebService(svc As ServiceDetails, payLoad As IList(Of String)) As Object
        Try
            'Importing wsdl file
            Dim metaExcClient As MetadataExchangeClient = New MetadataExchangeClient(svc.WSDLUri, MetadataExchangeClientMode.HttpGet)
            metaExcClient.ResolveMetadataReferences = True
            Dim metaDataSet As MetadataSet = metaExcClient.GetMetadata

            Dim wsdlImporter As WsdlImporter = New WsdlImporter(metaDataSet)
            'When you initialize the WsdlImporter with "MetaDataSet", the wsdlImporter.WsdlImpoertExtensions will
            ' already be loaded with 7 or so extensions. One of them being "XmlSerializerMessageContractImporter".
            ' Removing this extension seems to be solving the problem of duplicate Data Contracts and Compiler Error
            ' on Duplicate 'GeneratedCodeAttribute' etc.
            wsdlImporter.WsdlImportExtensions.Remove(GetType(XmlSerializerMessageContractImporter))

            'Trust All certificate in SSL Communication (HTTPS)
            'TODO

            'Extract Service And Data Contract Descriptions
            Dim contracts As New Collection(Of ContractDescription)
            contracts = wsdlImporter.ImportAllContracts()

            'Extract all the End Points
            Dim allEndPoints As ServiceEndpointCollection = wsdlImporter.ImportAllEndpoints()

            'CodeCompile Unit
            Dim codeCompUnit As CodeCompileUnit = wsdlImporter.State(GetType(CodeCompileUnit))

            'Generate ServiceContract
            Dim serviceContractGenerator As ServiceContractGenerator = New ServiceContractGenerator(codeCompUnit)

            For Each contract As ContractDescription In contracts
                serviceContractGenerator.GenerateServiceContractType(contract)
            Next

            'Generate Code file for Contract
            Dim codeGenOption As CodeGeneratorOptions = New CodeGeneratorOptions With {
           .BracingStyle = "C"
            }

            'Create compilerResult instance
            Dim compResult As CompilerResults = Nothing

            'Compile Dscriptions to assembly
            Dim assembly As Assembly = Nothing

            'Create compiler instance of a specific language
            Using codeDomProvider As CodeDomProvider = CodeDomProvider.CreateProvider("VB")
                'Compile the code file to in-memory assembly
                Dim compParams As CompilerParameters = New CompilerParameters(New String() {"System.dll",
                                                                              "System.ServiceModel.dll",
                                                                              "System.Runtime.Serialization.dll"})
                compParams.GenerateInMemory = True
                compParams.GenerateExecutable = False
                compParams.WarningLevel = 1

                compResult = codeDomProvider.CompileAssemblyFromDom(compParams, serviceContractGenerator.TargetCompileUnit)
            End Using

            'Creating
            If Not compResult.Errors.HasErrors Then
                assembly = compResult.CompiledAssembly
            Else
                For Each err As CompilerError In compResult.Errors
                    Console.WriteLine(err.ErrorNumber + ": " + err.ErrorText + " " + err.IsWarning + " " + err.Line)
                Next
                Throw New Exception("Compiler Errors - unable to build Web Service Assembly")
            End If

            'Extract all Contract End Points from service Contract
            Dim ctrEndPoints As IDictionary(Of String, IEnumerable(Of ServiceEndpoint)) = New Dictionary(Of String, IEnumerable(Of ServiceEndpoint))

            For Each ctrDes As ContractDescription In contracts
                'Get all the end-points for the given contractDescription
                Dim svcEndPoints As IList(Of ServiceEndpoint) = allEndPoints.Where(Function(x) x.Contract.Name = ctrDes.Name).ToList()
                ctrEndPoints.Add(ctrDes.Name, svcEndPoints)
            Next

            Dim currentSvcEndPoints As IEnumerable(Of ServiceEndpoint) ' = wsdlImporter.'Find the endpoint for the given contractName :TODO

            If ctrEndPoints.TryGetValue(svc.contractName, currentSvcEndPoints) Then
                'Find the endpoint of the service to which the proxy needs to contact
                Dim svcEndPoint As ServiceEndpoint = currentSvcEndPoints.First(Function(x) x.ListenUri.AbsoluteUri = svc.serviceUri.AbsoluteUri)

                'Generate Client Proxy
                Dim proxy = GetProxy(svc.contractName, svcEndPoint, assembly)

                'Deserialize each payLoad argument to Object
                Dim newPayLoad As IList(Of Object) = New List(Of Object)
                For Each pay In payLoad
                    Dim clrObj
                    Try
                        clrObj = Deserialize(pay.ToString(), assembly)
                    Catch ex As Exception
                        clrObj = pay
                    End Try
                    newPayLoad.Add(clrObj)
                Next

                'Find Operation contract on the proxy and invoke
                proxy.GetType().GetMethod(svc.methodName).Invoke(proxy, newPayLoad.ToArray())
            End If

        Catch ex As Exception
            Console.WriteLine(ex)
            Throw
        End Try
    End Function

    Private Function Deserialize(xml As String, assembly As Assembly) As Object
        Dim ctr As Type = GetDataContractType(xml, assembly)
        Return Deserialize(xml, ctr)
    End Function

    Private Function Deserialize(xml As String, toType As Type)
        Using stream As Stream = New MemoryStream
            Dim data As Array = System.Text.Encoding.UTF8.GetBytes(xml)
            stream.Write(data, 0, data.Length)
            stream.Position = 0
            Dim dataCtrSerial As DataContractSerializer = New DataContractSerializer(toType)
            Return dataCtrSerial.ReadObject(stream)
        End Using

    End Function

    Private Function GetDataContractType(xml As String, assembly As Assembly) As Type
        Dim serializedXML As XDocument = ConvertToXML(xml)
        Dim match = assembly.GetTypes().First(Function(x) x.Name = serializedXML.Root.Name.LocalName)
    End Function

    Private Function ConvertToXML(xml As String)

        Using stream As Stream = New MemoryStream
            Dim data As Array = System.Text.Encoding.UTF8.GetBytes(xml)
            stream.Write(data, 0, data.Length)
            stream.Position = 0
            Return XDocument.Load(stream)
        End Using

    End Function

    Private Function GetProxy(contractName As String, svcEndPoint As ServiceEndpoint, assembly As Assembly) As Object
        Dim proxyT As Type = assembly.GetTypes.First(Function(t) t.IsClass And
                                                         DBNull.Value.Equals(t.GetInterface(contractName)) And
                                                         DBNull.Value.Equals(t.GetInterface(GetType(ICommunicationObject).Name)))
        Dim proxy = assembly.CreateInstance(proxyT.Name, False, System.Reflection.BindingFlags.CreateInstance, Nothing,
                                            New Object() {svcEndPoint.Binding, svcEndPoint.Address}, CultureInfo.CurrentCulture, Nothing)
        Return proxy
    End Function

    Private Function GetEndPointsOfEachServiceContract(wsdlImporter As WsdlImporter, svcCtrDesc As Collection(Of ContractDescription))
        Dim allEndPoints As ServiceEndpointCollection = wsdlImporter.ImportAllEndpoints()
        Dim ctrEndPoints As IDictionary(Of String, IEnumerable(Of ServiceEndpoint)) = New Dictionary(Of String, IEnumerable(Of ServiceEndpoint))

        For Each ctrDesc As ContractDescription In svcCtrDesc
            'Get all the end-points for the given contractDescription
            Dim svcEndPoints As IList(Of ServiceEndpoint) = allEndPoints.Where(Function(x) x.Contract.Name = ctrDesc.Name).ToList()
            ctrEndPoints.Add(ctrDesc.Name, svcEndPoints)
        Next

        Return ctrEndPoints
    End Function

    Private Function GetAssembly(svcCtrDesc As Collection(Of ContractDescription)) As Assembly
        Dim codeCompUnit As CodeCompileUnit = GetServiceAndDataContractCompileUnitFromWSDL(svcCtrDesc)
        Dim CompResult As CompilerResults = GenerateContractsAssemblyInMemory(codeCompUnit)

        If Not CompResult.Errors.HasErrors Then
            Return CompResult.CompiledAssembly
        End If
        Return Nothing 'Returning un-initialized value
    End Function

    Private Function GenerateContractsAssemblyInMemory(codeCompUnit As CodeCompileUnit) As CompilerResults
        ' Generate a code file for contract
        Dim codeGenOption As CodeGeneratorOptions = New CodeGeneratorOptions With {
            .BracingStyle = "C"
        }
        Dim codeDomProv As CodeDomProvider = CodeDomProvider.CreateProvider("VB")

        'Compile the code file to in-memory assembly
        Dim compParams As CompilerParameters = New CompilerParameters(New String() {"System.dll",
                                                                      "System.ServiceModel.dll",
                                                                      "System.Runtime.Serialization.dll"})
        compParams.GenerateInMemory = True
        compParams.GenerateExecutable = False
        compParams.OutputAssembly = "WebServiceReflector.dll"
        Return codeDomProv.CompileAssemblyFromDom(compParams, codeCompUnit)



    End Function

    Private Function GetServiceAndDataContractCompileUnitFromWSDL(svcCtrDesc As Collection(Of ContractDescription)) As CodeCompileUnit
        Dim svcCtrGen As ServiceContractGenerator = New ServiceContractGenerator()

        For Each ctrDesc As ContractDescription In svcCtrDesc
            svcCtrGen.GenerateServiceContractType(ctrDesc)
        Next
        Return svcCtrGen.TargetCompileUnit
    End Function
End Class
