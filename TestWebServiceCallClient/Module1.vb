Module Module1

    Sub Main()
        'Service {http://xmlns.oracle.com/apps/scm/productHub/itemImport/batches/itemBatchMaintenanceService/}ItemBatchMaintenanceService
        'WSDL : http://slc11bxy.us.oracle.com:17806/egiItemImport/ItemBatchMaintenanceService?wsdl
        'End Point : http://slc11bxy.us.oracle.com:17806/egiItemImport/ItemBatchMaintenanceService
        'Service Name : ItemBatchMaintenanceService

        'Another WSDL : http://slc11gqi.us.oracle.com:10617/soa-infra/services/default/EgpDeleteGroupsComposite/processdeletegroup_client_ep?WSDL
        'End Point : http://slc11gqi.us.oracle.com:10617/soa-infra/services/default/EgpDeleteGroupsComposite/processdeletegroup_client_ep
        'Service Name: processdeletegroup_client_ep
        'Operation : process
        Dim WebServiceURL As String = "http://slc11gqi.us.oracle.com:10617/soa-infra/services/default/EgpDeleteGroupsComposite/processdeletegroup_client_ep?WSDL"

        'Specify Service Name
        Dim serviceName As String = "processdeletegroup_client_ep"

        'Specify Method Name
        Dim methodName As String = "process"

        'Argument passed to method, if it needs any
        ' For DeleteGroup, payload requires [ DeleteGroupSequenceId(long), ActionType(integer), ArchiveFlag(integer), DeleteGroupName(String) ]
        Dim arArguments(3) As String
        arArguments(0) = ""
        arArguments(1) = ""
        arArguments(2) = ""
        arArguments(3) = ""

        'Dim WebServiceURL As String = "http://www.xignite.com/xQuotes.asmx"
        'methodName = "GetQuickQuotes"

        Dim serviceDetails As ServiceDetails = New ServiceDetails(New Uri(WebServiceURL), New Uri(WebServiceURL), methodName, "a")
        Dim payLoad As IList(Of String)
        Dim objCallWS As New DynamicWebService
        Dim sessionID As String = objCallWS.CallWebService(serviceDetails, arArguments)


    End Sub

End Module
